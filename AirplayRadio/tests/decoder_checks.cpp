#include "../native/receiver.cpp"
#include <fstream>
#include <thread>
#include <stdexcept>
static void check(bool ok, const char* what) { if (!ok) throw std::runtime_error(what); }
int main() {
    try {
        Receiver r;
        std::vector<int16_t> pcm(8192);
        for (size_t i = 0; i < pcm.size(); ++i) pcm[i] = i % 2 ? -8192 : 8192;
        audio_decode_struct packet{}; packet.ct = 1;
        packet.data = reinterpret_cast<unsigned char*>(pcm.data()); packet.data_len = pcm.size() * 2;
        for (int i = 0; i < 6; ++i) audio(&r, nullptr, &packet);
        std::vector<float> out(2048);
        check(ar_read(&r, out.data(), out.size()) == 2048, "PCM decode produces frames");
        check(std::abs(out[0] - .25f) < .001f && std::abs(out[1] + .25f) < .001f, "stereo order and amplitude");
        volume(&r, -6.0206f); ar_read(&r, out.data(), out.size());
        check(std::abs(out[0] - .125f) < .001f, "sender volume");
        for (int i = 0; i < 100; ++i) audio(&r, nullptr, &packet);
        check(r.count <= r.ring.size() && r.dropped > 0, "bounded overflow");
        std::atomic<bool> locked{false}, release{false};
        std::thread writer([&] { std::lock_guard<std::mutex> guard(r.ring_lock); locked = true; while (!release) std::this_thread::yield(); });
        while (!locked) std::this_thread::yield();
        int read = ar_read(&r, out.data(), out.size()); release = true; writer.join();
        check(read == 0 && out[0] == 0 && ar_stat(&r, 2) == 1, "audio callback never waits for producer; contention counted");
        flush(&r); check(ar_read(&r, out.data(), out.size()) == 0 && out[0] == 0, "flush removes stale audio");
        unsigned char good[] = {'m','l','i','t',0,0,0,12,'m','i','n','m',0,0,0,4,'T','e','s','t'};
        metadata(&r, good, sizeof(good)); check(r.title == "Test", "nested metadata");
        unsigned char bad[] = {'m','i','n','m',255,255,255,255};
        metadata(&r, bad, sizeof(bad)); check(r.title == "Test", "truncated metadata rejected");
        const unsigned char art[] = {137,80,78,71,1,2,3,4};
        coverart(&r, art, sizeof(art));
        auto revision = ar_cover_revision(&r);
        unsigned char copied[sizeof(art)]{};
        check(ar_cover(&r, revision, nullptr, 0) == sizeof(art), "cover size query");
        check(ar_cover(&r, revision, copied, sizeof(copied) - 1) == -1, "short cover buffer rejected");
        check(ar_cover(&r, revision, copied, sizeof(copied)) == sizeof(art) && !memcmp(art, copied, sizeof(art)), "exact cover bytes copied");
        clear_cover(&r);
        check(ar_cover(&r, revision, copied, sizeof(copied)) == -1, "stale cover revision rejected");
        check(ar_cover(&r, ar_cover_revision(&r), nullptr, 0) == 0, "cover clear propagated");
        coverart(&r, art, 2 * 1024 * 1024 + 1);
        check(ar_cover(&r, ar_cover_revision(&r), nullptr, 0) == 0, "oversized cover not copied");
        r.clear(); std::vector<float> reserve(44100, .2f); r.push(reserve.data(), reserve.size());
        locked = false; release = false;
        std::thread cover_reader([&] { std::lock_guard<std::mutex> guard(r.cover_lock); locked = true; while (!release) std::this_thread::yield(); });
        while (!locked) std::this_thread::yield();
        read = ar_read(&r, out.data(), out.size()); release = true; cover_reader.join();
        check(read == out.size(), "artwork lock cannot block audio output");
        r.clear();
        std::ifstream file(std::string(FIXTURE_DIR) + "/tone.alac", std::ios::binary);
        std::vector<unsigned char> alac((std::istreambuf_iterator<char>(file)), {});
        check(!alac.empty(), "ALAC fixture exists");
        check(open_decoder(&r, 2, 4096), "ALAC decoder initialization");
        packet.ct = 2; packet.data = alac.data(); packet.data_len = alac.size();
        uint64_t before = r.decoded;
        for (int i = 0; i < 6; ++i) audio(&r, nullptr, &packet);
        check(r.decoded > before, "actual compressed ALAC decoded");
        volume(&r, 0); check(ar_read(&r, out.data(), out.size()) == 2048, "ALAC read");
        double energy = 0; for (auto f : out) energy += f*f;
        check(energy > 0.01, "ALAC contains audible waveform");
        // A sustained consumer with short producer gaps must not run out of the new reserve.
        r.clear(); std::vector<float> steady(44100, .2f); r.push(steady.data(), steady.size());
        std::vector<float> block(882); // 10ms stereo
        for (int i = 0; i < 2000; ++i) {
            if (i % 20 == 0) r.push(steady.data(), 17640); // 200ms batches
            check(ar_read(&r, block.data(), block.size()) == 882, "short delivery gaps absorbed");
            check(std::abs(block[0] - .2f) < .001f, "continuous waveform during burst delivery");
        }
        check(ar_stat(&r, 1) == 0, "no underruns during bounded delivery gaps");
        while (ar_read(&r, block.data(), block.size()) == 882) {}
        check(ar_stat(&r, 1) == 1, "real starvation counted once then rebuffered");
        r.clear(); check(ar_stat(&r, 0) == 0, "queue telemetry clears");
        const void* old_conn = reinterpret_cast<void*>(1); const void* new_conn = reinterpret_cast<void*>(2);
        remote_control(&r, old_conn, "192.168.1.10", "ABCD", "1234");
        remote_control(&r, new_conn, "192.168.1.11", "BEEF", "5678");
        remote_closed(&r, old_conn); check(r.remote_id == "BEEF", "old connection close cannot clear new controls");
        remote_control(&r, old_conn, "192.168.1.10", "ABCD", "1234\r\nInjected: yes");
        check(r.remote_id == "BEEF", "remote header injection rejected");
        remote_closed(&r, new_conn); check(r.remote_id.empty(), "active connection close clears remote identity");
        resend_requested(&r, 3); check(ar_stat(&r, 4) == 3, "RTP resend diagnostics");
        check(open_decoder(&r, 8, 480), "AAC-ELD decoder initialization");
        check(open_decoder(&r, 4, 1024), "AAC-LC decoder initialization");
        r.reset_decoder();
        puts("PASS: PCM/ALAC decoding, AAC setup, volume, bounded queue, nonblocking read, metadata and versioned artwork bounds/clear/concurrency");
        return 0;
    } catch (const std::exception& e) { fprintf(stderr, "FAIL: %s\n", e.what()); return 1; }
}
