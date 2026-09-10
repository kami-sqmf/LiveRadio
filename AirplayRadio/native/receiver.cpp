#include <winsock2.h>
#include <windows.h>
#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>
#include "receiver.h"
extern "C" {
#include "raop.h"
#include <libavcodec/avcodec.h>
#include <libavutil/channel_layout.h>
#include <libswresample/swresample.h>
}

struct Receiver {
    raop_t* raop = nullptr;
    bool initialized = false;
    dnssd_t* dns = nullptr;
    unsigned short port = 0;
    std::mutex audio, ring_lock, text_lock, cover_lock;
    std::vector<unsigned char> cover;
    std::atomic<uint64_t> cover_revision{0};
    AVCodecContext* decoder = nullptr;
    SwrContext* resampler = nullptr;
    AVFrame* frame = nullptr;
    AVPacket* packet = nullptr;
    int codec = 0;
    std::vector<float> scratch = std::vector<float>(32768);
    std::vector<float> ring = std::vector<float>(44100 * 2 * 2);
    size_t head = 0, count = 0;
    std::atomic<size_t> queued{0};
    bool buffering = true;
    std::atomic<float> gain{1.0f};
    std::atomic<uint64_t> decoded{0}, dropped{0};
    std::atomic<uint64_t> underruns{0}, contention{0}, decode_errors{0}, resends{0}, last_audio{0}, max_gap{0};
    const void* remote_connection = nullptr;
    uint64_t remote_epoch = 0;
    std::string remote_peer, remote_id, remote_token;
    std::string status = "Stopped", title, artist, client;
    void message(const std::string& s) { std::lock_guard<std::mutex> l(text_lock); status = s.substr(0, 240); }
    void reset_decoder() {
        swr_free(&resampler); avcodec_free_context(&decoder);
        av_frame_free(&frame); av_packet_free(&packet); codec = 0;
    }
    void clear() { std::lock_guard<std::mutex> l(ring_lock); head = count = 0; queued = 0; buffering = true; }
    void push(const float* data, size_t n) {
        std::lock_guard<std::mutex> l(ring_lock);
        if (n > ring.size()) { data += n - ring.size(); n = ring.size(); }
        if (count + n > ring.size()) {
            size_t discard = count + n - ring.size();
            head = (head + discard) % ring.size(); count -= discard; dropped += discard;
        }
        size_t tail = (head + count) % ring.size();
        size_t first = std::min(n, ring.size() - tail);
        memcpy(ring.data() + tail, data, first * sizeof(float));
        memcpy(ring.data(), data + first, (n - first) * sizeof(float));
        count += n; queued = count; decoded += n;
    }
};

static void nothing(void*) {}
static void coverart(void* p, const void* data, int size) {
    auto r = static_cast<Receiver*>(p);
    std::lock_guard<std::mutex> l(r->cover_lock);
    // Decode/resizing happens in a managed background worker, never on the RTP worker.
    try {
        if (!data || size <= 0 || size > 2 * 1024 * 1024) r->cover.clear();
        else {
            const auto bytes = static_cast<const unsigned char*>(data);
            r->cover.assign(bytes, bytes + size);
        }
    } catch (...) { r->cover.clear(); }
    ++r->cover_revision;
}
static void clear_cover(void* p) { coverart(p, nullptr, 0); }
static bool remote_value(const char* s, size_t max, const char* alphabet) {
    if (!s || !*s || strlen(s) > max) return false;
    return strspn(s, alphabet) == strlen(s);
}
static void remote_control(void* p, const void* conn, const char* peer, const char* id, const char* token) {
    if (!remote_value(id, 16, "0123456789abcdefABCDEF") ||
        !remote_value(token, 32, "0123456789") ||
        !remote_value(peer, 79, "0123456789abcdefABCDEF:.%")) return;
    auto r = static_cast<Receiver*>(p);
    std::lock_guard<std::mutex> l(r->text_lock);
    r->remote_connection = conn; ++r->remote_epoch;
    r->remote_peer = peer; r->remote_id = id; r->remote_token = token;
}
static void remote_closed(void* p, const void* conn) {
    auto r = static_cast<Receiver*>(p);
    std::lock_guard<std::mutex> l(r->text_lock);
    if (r->remote_connection != conn) return;
    r->remote_connection = nullptr; ++r->remote_epoch;
    r->remote_peer.clear(); r->remote_id.clear(); r->remote_token.clear();
}
static void resend_requested(void* p, unsigned int n) { static_cast<Receiver*>(p)->resends += n; }
static void reset(void* p, reset_type_t) { static_cast<Receiver*>(p)->clear(); }
static void conn_reset(void* p, int) { static_cast<Receiver*>(p)->message("Connection reset; reconnect from your phone"); }
static void video(void*, raop_ntp_t*, video_decode_struct*) {}
static int video_codec(void*, video_codec_t) { return -1; }
static void log_message(void* p, int level, const char* s) {
    if (level <= 3 && s) static_cast<Receiver*>(p)->message(s);
}
static void volume(void* p, float db) {
    if (std::isfinite(db)) static_cast<Receiver*>(p)->gain = db <= -144 ? 0.0f : std::pow(10.0f, std::clamp(db, -30.0f, 0.0f) / 20.0f);
}
static double initial_volume(void*) { return 0.0; }
static void client_request(void* p, char*, char*, char* name, bool* admit) {
    auto r = static_cast<Receiver*>(p);
    std::lock_guard<std::mutex> l(r->text_lock);
    r->client = name ? std::string(name).substr(0, 160) : "Phone";
    *admit = true;
}
static void flush(void* p) {
    auto r = static_cast<Receiver*>(p);
    std::lock_guard<std::mutex> l(r->audio);
    if (r->decoder) avcodec_flush_buffers(r->decoder);
    swr_free(&r->resampler); r->clear();
}
static uint32_t be32(const unsigned char* p) {
    return uint32_t(p[0]) << 24 | uint32_t(p[1]) << 16 | uint32_t(p[2]) << 8 | p[3];
}
static void parse_metadata(Receiver* r, const unsigned char* p, size_t size, int depth) {
    if (depth > 4) return;
    while (size >= 8) {
        uint32_t n = be32(p + 4);
        if (n > size - 8) return;
        if (!memcmp(p, "mlit", 4)) parse_metadata(r, p + 8, n, depth + 1);
        else if (!memcmp(p, "minm", 4)) r->title.assign(reinterpret_cast<const char*>(p + 8), std::min(n, 240u));
        else if (!memcmp(p, "asar", 4)) r->artist.assign(reinterpret_cast<const char*>(p + 8), std::min(n, 240u));
        p += 8 + n; size -= 8 + n;
    }
}
static void metadata(void* p, const void* data, int size) {
    if (!data || size <= 0 || size > 65536) return;
    auto r = static_cast<Receiver*>(p);
    std::lock_guard<std::mutex> l(r->text_lock);
    parse_metadata(r, static_cast<const unsigned char*>(data), size, 0);
}

static bool open_decoder(Receiver* r, int ct, int spf) {
    r->reset_decoder(); r->clear();
    AVCodecID id = ct == 2 ? AV_CODEC_ID_ALAC : ct == 8 || ct == 4 ? AV_CODEC_ID_AAC : ct == 1 ? AV_CODEC_ID_PCM_S16LE : AV_CODEC_ID_NONE;
    const AVCodec* codec = avcodec_find_decoder(id);
    if (!codec) { r->message("Unsupported AirPlay audio codec"); return false; }
    r->decoder = avcodec_alloc_context3(codec);
    r->frame = av_frame_alloc(); r->packet = av_packet_alloc();
    if (!r->decoder || !r->frame || !r->packet) { r->reset_decoder(); return false; }
    auto d = r->decoder;
    d->sample_rate = 44100; av_channel_layout_default(&d->ch_layout, 2);
    d->thread_count = 1;
    unsigned char cookie[] = {0,0,0,36,'a','l','a','c',0,0,0,0,0,0,1,96,0,16,40,10,14,2,0,255,0,0,0,0,0,0,0,0,0,0,172,68};
    unsigned char eld[] = {0xf8,0xe8,0x50,0x00}, lc[] = {0x12,0x10};
    if (spf <= 0 || spf > 4096) spf = 352;
    cookie[12] = spf >> 24; cookie[13] = spf >> 16; cookie[14] = spf >> 8; cookie[15] = spf;
    const unsigned char* extra = ct == 2 ? cookie : ct == 8 ? eld : lc;
    int len = ct == 2 ? sizeof(cookie) : ct == 8 ? sizeof(eld) : ct == 4 ? sizeof(lc) : 0;
    if (len) {
        d->extradata = static_cast<uint8_t*>(av_mallocz(len + AV_INPUT_BUFFER_PADDING_SIZE));
        if (!d->extradata) { r->reset_decoder(); return false; }
        memcpy(d->extradata, extra, len); d->extradata_size = len;
    }
    if (avcodec_open2(d, codec, nullptr) < 0) { r->message("Audio decoder initialization failed"); r->reset_decoder(); return false; }
    r->codec = ct; r->message("Receiving audio"); return true;
}
static void format(void* p, unsigned char* ct, unsigned short* spf, bool*, bool*, uint64_t*) {
    auto r = static_cast<Receiver*>(p); std::lock_guard<std::mutex> l(r->audio);
    open_decoder(r, *ct, *spf);
}
static void audio(void* p, raop_ntp_t*, audio_decode_struct* data) {
    if (!data || !data->data || data->data_len <= 0 || data->data_len > 65536) return;
    auto r = static_cast<Receiver*>(p); std::lock_guard<std::mutex> l(r->audio);
    uint64_t now = GetTickCount64(), previous = r->last_audio.exchange(now);
    if (previous && now > previous) {
        uint64_t gap = now - previous, old = r->max_gap.load();
        while (gap > old && !r->max_gap.compare_exchange_weak(old, gap)) {}
    }
    if (r->codec != data->ct && !open_decoder(r, data->ct, 352)) return;
    av_packet_unref(r->packet);
    if (av_new_packet(r->packet, data->data_len) < 0) { ++r->decode_errors; return; }
    memcpy(r->packet->data, data->data, data->data_len);
    if (avcodec_send_packet(r->decoder, r->packet) < 0) { ++r->decode_errors; return; }
    int result;
    while ((result = avcodec_receive_frame(r->decoder, r->frame)) == 0) {
        auto f = r->frame;
        if (!r->resampler) {
            AVChannelLayout stereo = AV_CHANNEL_LAYOUT_STEREO;
            if (swr_alloc_set_opts2(&r->resampler, &stereo, AV_SAMPLE_FMT_FLT, 44100,
                &f->ch_layout, static_cast<AVSampleFormat>(f->format), f->sample_rate, 0, nullptr) < 0 || swr_init(r->resampler) < 0) {
                swr_free(&r->resampler); r->message("Audio conversion failed"); break;
            }
        }
        uint8_t* out[] = { reinterpret_cast<uint8_t*>(r->scratch.data()) };
        int frames = swr_convert(r->resampler, out, r->scratch.size() / 2,
            const_cast<const uint8_t**>(f->extended_data), f->nb_samples);
        if (frames > 0) r->push(r->scratch.data(), frames * 2);
        av_frame_unref(f);
    }
    if (result != AVERROR(EAGAIN) && result != AVERROR_EOF) ++r->decode_errors;
}

AR_API void* ar_create() { try { return new Receiver; } catch (...) { return nullptr; } }
AR_API int ar_start(void* handle, const char* name, const char* device_id, const char* key_path) {
    auto r = static_cast<Receiver*>(handle);
    if (!r || r->raop || !name || strlen(name) > 48 || !device_id || !key_path) return -1;
    unsigned int mac[6];
    if (sscanf(device_id, "%02x:%02x:%02x:%02x:%02x:%02x", mac, mac+1, mac+2, mac+3, mac+4, mac+5) != 6) return -2;
    char hw[6]; for (int i = 0; i < 6; i++) hw[i] = static_cast<char>(mac[i]);
    raop_callbacks_t cb{};
    cb.cls = r; cb.audio_process = audio; cb.video_process = video;
    cb.audio_get_format = format; cb.audio_flush = flush;
    cb.video_flush = nothing; cb.video_pause = nothing; cb.video_resume = nothing;
    cb.video_reset = reset; cb.video_set_codec = video_codec;
    cb.conn_init = nothing; cb.conn_destroy = nothing; cb.conn_feedback = nothing; cb.conn_reset = conn_reset;
    cb.audio_set_volume = volume; cb.audio_set_client_volume = initial_volume;
    cb.audio_set_metadata = metadata; cb.report_client_request = client_request;
    cb.audio_set_coverart = coverart; cb.audio_stop_coverart_rendering = clear_cover;
    cb.remote_control = remote_control; cb.remote_control_closed = remote_closed;
    cb.audio_resend_requested = resend_requested;
    r->raop = raop_init(&cb);
    if (!r->raop) return -3;
    raop_set_log_callback(r->raop, log_message, r); raop_set_log_level(r->raop, 3);
    if (raop_init2(r->raop, 0, device_id, key_path)) return -4;
    r->initialized = true;
    int error = 0;
    r->dns = dnssd_init(name, static_cast<int>(strlen(name)), hw, 6, &error, 0);
    if (!r->dns || error) return -5;
    // Audio-only advertisement: no video, photos, mirroring or HLS queue.
    for (int bit : {0,1,2,3,4,5,7,8,13,33,34,40,41,42}) dnssd_set_airplay_features(r->dns, bit, 0);
    raop_set_dnssd(r->raop, r->dns);
    raop_set_lang(r->raop, "en");
    if (raop_start_httpd(r->raop, &r->port) < 0) return -6;
    raop_set_port(r->raop, r->port);
    if (dnssd_register_raop(r->dns, r->port) || dnssd_register_airplay(r->dns, r->port)) return -7;
    r->message("Waiting for AirPlay — select this receiver on your phone"); return 0;
}
AR_API void ar_destroy(void* handle) {
    auto r = static_cast<Receiver*>(handle); if (!r) return;
    // RAOP workers must finish before freeing the DNS records or decoder.
    if (r->raop && r->initialized) raop_stop_httpd(r->raop);
    if (r->dns) {
        dnssd_unregister_raop(r->dns); dnssd_unregister_airplay(r->dns);
        dnssd_destroy(r->dns);
    }
    if (r->raop) raop_destroy(r->raop);
    r->reset_decoder(); delete r;
}
AR_API void ar_clear(void* handle) { if (handle) static_cast<Receiver*>(handle)->clear(); }
AR_API int ar_read(void* handle, float* output, int samples) {
    if (!output || samples <= 0) return 0;
    std::fill(output, output + samples, 0.0f);
    auto r = static_cast<Receiver*>(handle); if (!r || samples % 2) return 0;
    std::unique_lock<std::mutex> l(r->ring_lock, std::try_to_lock);
    if (!l.owns_lock()) { ++r->contention; return 0; }
    // Half a second absorbs short delivery/scheduling gaps; memory remains bounded at 2s.
    size_t threshold = std::min(r->ring.size(), std::max<size_t>(44100, samples));
    if (r->buffering && r->count < threshold) return 0;
    r->buffering = false;
    size_t n = std::min(static_cast<size_t>(samples), r->count);
    float gain = r->gain;
    size_t first = std::min(n, r->ring.size() - r->head);
    memcpy(output, r->ring.data() + r->head, first * sizeof(float));
    memcpy(output + first, r->ring.data(), (n - first) * sizeof(float));
    r->head = (r->head + n) % r->ring.size(); r->count -= n;
    r->queued = r->count;
    if (n < static_cast<size_t>(samples)) { r->buffering = true; ++r->underruns; }
    l.unlock();
    for (size_t i = 0; i < n; ++i) output[i] *= gain;
    return static_cast<int>(n);
}
AR_API int ar_text(void* handle, int field, char* output, int capacity) {
    if (!handle || !output || capacity < 1) return 0;
    auto r = static_cast<Receiver*>(handle); std::lock_guard<std::mutex> l(r->text_lock);
    std::string remote;
    if (field == 4) remote = std::to_string(r->remote_epoch) + "\n" + r->remote_peer + "\n" + r->remote_id + "\n" + r->remote_token;
    const auto& s = field == 4 ? remote : field == 1 ? r->title : field == 2 ? r->artist : field == 3 ? r->client : r->status;
    int n = std::min(static_cast<int>(s.size()), capacity - 1); memcpy(output, s.data(), n); output[n] = 0; return n;
}
AR_API int ar_port(void* handle) { return handle ? static_cast<Receiver*>(handle)->port : 0; }
AR_API uint64_t ar_decoded_samples(void* h) { return h ? static_cast<Receiver*>(h)->decoded.load() : 0; }
AR_API uint64_t ar_dropped_samples(void* h) { return h ? static_cast<Receiver*>(h)->dropped.load() : 0; }
AR_API uint64_t ar_cover_revision(void* h) { return h ? static_cast<Receiver*>(h)->cover_revision.load() : 0; }
AR_API int ar_cover(void* h, uint64_t revision, unsigned char* output, int capacity) {
    if (!h) return -1;
    auto r = static_cast<Receiver*>(h); std::lock_guard<std::mutex> l(r->cover_lock);
    if (revision != r->cover_revision.load()) return -1;
    int size = static_cast<int>(r->cover.size());
    if (output) {
        if (capacity < size) return -1;
        if (size) memcpy(output, r->cover.data(), size);
    }
    return size;
}
AR_API uint64_t ar_stat(void* h, int field) {
    if (!h) return 0;
    auto r = static_cast<Receiver*>(h);
    switch (field) {
        case 0: return r->queued;
        case 1: return r->underruns;
        case 2: return r->contention;
        case 3: return r->decode_errors;
        case 4: return r->resends;
        case 5: return r->max_gap;
        case 6: return r->dropped;
        default: return 0;
    }
}
