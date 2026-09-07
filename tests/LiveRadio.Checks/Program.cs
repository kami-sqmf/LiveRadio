using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveRadio.Core;

internal static class Program
{
    private static int _checks;
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        _checks++;
        Console.WriteLine("PASS: " + message);
    }

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--mp3-format-fixtures"))
            {
                int index = Array.IndexOf(args, "--mp3-format-fixtures");
                await Mp3RestartChecks.RunFormatChange(args[index + 1], args[index + 2], Assert);
                Console.WriteLine("Completed " + _checks + " MP3 format checks."); return 0;
            }
            if (args.Contains("--mp3-restart-fixture"))
            {
                await Mp3RestartChecks.Run(args[Array.IndexOf(args, "--mp3-restart-fixture") + 1], Assert);
                Console.WriteLine("Completed " + _checks + " MP3 restart checks."); return 0;
            }
            if (args.Contains("--audio-callback-stress"))
            {
                await AudioCallbackChecks.Run(Assert); return 0;
            }
            if (args.Contains("--continuity"))
            {
                int start = Array.IndexOf(args, "--continuity");
                int formatArg = Array.IndexOf(args, "--format");
                var format = formatArg < 0 ? StreamFormat.Hls : (StreamFormat)Enum.Parse(typeof(StreamFormat), args[formatArg + 1], true);
                await ContinuityChecks.Run(new Uri(args[start + 1]), format, int.Parse(args[start + 2]), args.Contains("--strict"), Assert);
                return 0;
            }
            if (args.Contains("--media-fixtures"))
            {
                await MediaChecks.Run(args[Array.IndexOf(args, "--media-fixtures") + 1], Assert);
                Console.WriteLine("Completed " + _checks + " media checks."); return 0;
            }
            if (args.Contains("--catalog-live"))
            {
                await CheckCatalogLive();
                Console.WriteLine("Completed " + _checks + " catalog checks.");
                return 0;
            }
            string path = RadioBrowserClient.BuildSearchPath("A&B 中文", " tw ");
            Assert(path.Contains("name=A%26B%20") && path.EndsWith("countrycode=TW"), "Search escapes terms and normalizes country codes");
            Assert(!RadioBrowserClient.BuildSearchPath("", "").Contains("countrycode="), "Worldwide search omits the country filter instead of matching blank countries");
            Assert(!path.Contains("codec=") && path.Contains("limit=100"), "Search does not hide AAC and OGG at the API");
            bool rejected = false;
            try { RadioBrowserClient.BuildSearchPath("", "TW&limit=999"); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "Invalid country cannot inject query parameters");

            var station = new RadioStation { Id = Guid.NewGuid().ToString(), Name = "Sample", Codec = "MP3", Url = "https://example.com/live" };
            Assert(station.IsSupported, "Direct MP3 station is accepted");
            station.Hls = 1;
            Assert(station.IsSupported && station.Format == StreamFormat.Hls, "HLS reaches the external decoder regardless of reported codec");
            station.Hls = 0;
            foreach (string codec in new[] { "AAC", "AAC+", "HE-AAC", "OGG", "Vorbis", "Opus" })
            {
                station.Codec = codec;
                Assert(station.IsSupported && station.Format != StreamFormat.Mp3, codec + " station reaches the external decoder");
            }
            station.Codec = "UNKNOWN";
            Assert(!station.IsSupported, "Unknown codec is not presented as playable");
            station.Codec = "MP3"; station.ResolvedUrl = "file:///C:/local.mp3";
            Assert(!station.IsSupported, "Local file URLs cannot enter the stream player");
            station.ResolvedUrl = "https://user:password@example.com/live";
            Assert(!station.IsSupported, "Embedded URL credentials are rejected");
            station.ResolvedUrl = "https://example.com/resolved";
            using (var json = new MemoryStream())
            {
                JsonData.Write(json, new[] { station }); json.Position = 0;
                var restored = JsonData.Read<RadioStation[]>(json);
                Assert(restored[0].StreamUri.AbsoluteUri.EndsWith("resolved"), "Station persistence preserves resolved URLs");
            }

            CheckSearchPages();
            await CatalogChecks.Run(Assert);
            await ReleaseChecks.Run(Assert);
            await BufferContinuityChecks.Run(Assert);
            await AudioCallbackChecks.Run(Assert);
            var pcmBytes = new byte[16000 * sizeof(float)];
            var expected = Enumerable.Range(0, 16000).Select(i => (float)i / 16000).ToArray();
            Buffer.BlockCopy(expected, 0, pcmBytes, 0, pcmBytes.Length);
            var fragmentedPcm = new PcmBuffer(8000, 2, 1, 1, lossless: true);
            using (var fragments = new FragmentedStream(pcmBytes))
                FfmpegDecoder.CopyPcm(fragments, fragmentedPcm, CancellationToken.None, () => { });
            var actual = new float[16000];
            Assert(fragmentedPcm.Read(actual) == actual.Length && actual.SequenceEqual(expected), "One-byte PCM reads preserve floats and complete stereo frames");
            rejected = false;
            try { FfmpegDecoder.CopyPcm(new MemoryStream(new byte[7]), fragmentedPcm, CancellationToken.None, () => { }); }
            catch (EndOfStreamException) { rejected = true; }
            Assert(rejected, "Truncated decoded PCM fails explicitly");

            using (var missing = new LiveStreamSession(new Uri("https://example.com/live"), StreamFormat.Aac,
                Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")))
            {
                Assert(await Task.WhenAny(missing.Completion, Task.Delay(5000)) == missing.Completion,
                    "Missing decoder fails promptly instead of reconnecting repeatedly");
                await missing.Completion;
                Assert(missing.State == StreamState.Failed && missing.Error.Contains("FFmpeg"), "Missing decoder produces an actionable setting message");
            }

            var icy = new List<byte>();
            icy.AddRange(Encoding.ASCII.GetBytes("abcde"));
            byte[] metadata = Encoding.UTF8.GetBytes("StreamTitle='Test song';");
            icy.Add(2); icy.AddRange(metadata); icy.AddRange(new byte[32 - metadata.Length]);
            icy.AddRange(Encoding.ASCII.GetBytes("fghij")); icy.Add(0);
            icy.AddRange(Encoding.ASCII.GetBytes("klm"));
            string title = null;
            using (var filtered = new IcyAudioStream(new FragmentedStream(icy.ToArray()), 5, t => title = t))
            using (var audio = new MemoryStream())
            {
                filtered.CopyTo(audio);
                Assert(Encoding.ASCII.GetString(audio.ToArray()) == "abcdefghijklm", "Fragmented ICY metadata is removed without losing audio bytes");
                Assert(title == "Test song", "ICY track title is extracted");
                Assert(filtered.Position == 13 && !filtered.CanSeek, "Decoder sees the audio byte position without ICY metadata or seeking");
            }
            using (var filtered = new IcyAudioStream(new MemoryStream(new byte[] { 1, 2, 2, 3 }), 2))
            {
                rejected = false;
                try { filtered.CopyTo(Stream.Null); } catch (EndOfStreamException) { rejected = true; }
                Assert(rejected, "Truncated metadata fails explicitly");
            }
            var ring = new PcmBuffer(8000, 2, 1, 1);
            var samples = Enumerable.Range(0, 32000).Select(i => (float)i).ToArray();
            ring.Write(samples, 1000);
            var output = new float[200];
            Assert(ring.Read(output) == 0 && output.All(x => x == 0), "Playback waits for the initial buffer");
            ring.Write(samples, samples.Length);
            Assert(ring.Available == 16000, "Buffer stays bounded during a burst");
            Assert(ring.Read(output) == 200 && output[0] == 16000 && output[199] == 16199, "Overflow retains the newest complete stereo frames");
            var drain = new float[16000]; ring.Read(drain);
            Assert(ring.IsBuffering && drain[15999] == 0, "Underrun produces silence and resumes buffering");
            var concurrent = new PcmBuffer(8000, 2, 1, 1);
            var producer = Task.Run(() => { for (int i = 0; i < 10000; i++) concurrent.Write(samples, 1000); });
            var consumer = Task.Run(() => { var audio = new float[512]; for (int i = 0; i < 10000; i++) concurrent.Read(audio); });
            await Task.WhenAll(producer, consumer);
            Assert(concurrent.Available >= 0 && concurrent.Available <= 16000 && concurrent.Available % 2 == 0, "Concurrent producer and audio callback preserve bounds and channel alignment");

            if (args.Contains("--live")) { await CheckLive(); await CheckCatalogLive(); }
            Console.WriteLine("Completed " + _checks + " checks.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void CheckSearchPages()
    {
        RadioStation Make(string url, string codec = "AAC", int hls = 0) => new RadioStation
        { Id = Guid.NewGuid().ToString(), Name = "Station", Url = url, Codec = codec, Hls = hls };
        var firstPage = Enumerable.Range(0, 100).Select(i => Make("https://example.com/" + i, "UNKNOWN", 0)).ToArray();
        var secondPage = new[] { Make("https://example.com/live"), Make("https://example.com/live"),
            Make("https://example.com/Live"), Make("https://example.com/live?token=2", "OGG") };
        int calls = 0;
        var result = RadioBrowserClient.ReadPages(offset => { calls++; return offset == 0 ? firstPage : secondPage; }, CancellationToken.None);
        Assert(calls == 2 && result.Stations.Length == 3, "Search pages past unsupported results and merges duplicate stream URLs");
        Assert(result.HlsCount == 0 && result.OtherUnsupportedCount == 100 && !result.Limited,
            "Search reports unsupported formats without claiming a truncated result");
        Assert(result.Stations.Any(s => s.Url.EndsWith("/Live")) && result.Stations.Any(s => s.Url.Contains("?token=2")),
            "Deduplication preserves case-sensitive paths and distinct query parameters");
        calls = 0;
        result = RadioBrowserClient.ReadPages(offset => { calls++; return firstPage; }, CancellationToken.None);
        Assert(calls == 5 && result.Limited, "An unsupported-only directory search stops at five pages");
        using (var cancel = new CancellationTokenSource())
        {
            calls = 0;
            bool canceled = false;
            try { RadioBrowserClient.ReadPages(offset => { calls++; cancel.Cancel(); return firstPage; }, cancel.Token); }
            catch (OperationCanceledException) { canceled = true; }
            Assert(canceled && calls == 1, "Canceled searches do not fetch another page");
        }
    }

    private static async Task CheckLive()
    {
        Assert(FfmpegDecoder.FindExecutable() != null, "An installed FFmpeg binary is available for AAC/OGG checks");
        int[] existingDecoders = Process.GetProcessesByName("ffmpeg").Select(p => { using (p) return p.Id; }).ToArray();
        using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(180)))
        {
            var browser = new RadioBrowserClient();
            var result = await browser.SearchDetailedAsync("", "TW", cancel.Token);
            Assert(result.Stations.Length > 20 && result.Stations.All(s => s.IsSupported), "Taiwan search includes more than the old 20-station cap");
            Assert(result.Stations.Any(s => s.Name == "中廣音樂網" && s.Format == StreamFormat.Aac),
                "The general Taiwan station list includes 中廣音樂網 (AAC)");
            Assert(result.Stations.Select(s => s.StreamUri.AbsoluteUri).Distinct().Count() == result.Stations.Length,
                "Real station results contain no duplicate stream URLs");
            var bcc = (await browser.SearchAsync("中廣音樂網", "TW", cancel.Token)).FirstOrDefault(s => s.Name == "中廣音樂網");
            Assert(bcc != null, "Exact 中廣音樂網 search finds the reported missing station");
            Assert(await DecodeStation(bcc, cancel.Token), "中廣音樂網 AAC produces audible PCM");
            var mp3 = result.Stations.Where(s => s.Format == StreamFormat.Mp3).OrderByDescending(s => s.Name.Contains("古典")).Take(3);
            bool mp3Decoded = false;
            foreach (var station in mp3)
            {
                if (await DecodeStation(station, cancel.Token)) { mp3Decoded = true; break; }
            }
            Assert(mp3Decoded, "Existing MP3 playback still works");
            foreach (string name in new[] { "PowerFM Dublin 320Kbps Ogg/Vorbis", "Deutschlandfunk | DLF | OPUS 24k" })
            {
                var station = (await browser.SearchAsync(name, "", cancel.Token)).FirstOrDefault(s => s.Format == StreamFormat.Ogg);
                Assert(station != null, name + " appears in OGG search results");
                Assert(await DecodeStation(station, cancel.Token), name + " decodes successfully");
            }
            await CheckStalledDecoder();
            var leftover = Process.GetProcessesByName("ffmpeg").Select(p => { using (p) return p.Id; }).Except(existingDecoders).ToArray();
            Assert(leftover.Length == 0, "All FFmpeg processes launched during streaming checks have exited");
        }
    }

    private static async Task CheckCatalogLive()
    {
        var browser = new RadioBrowserClient();
        using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
        {
            var bcc = await browser.BrowseAsync(new CatalogQuery { Name = "BCC", Country = "TW" }, null, cancel.Token);
            Assert(bcc.Stations.Any(s => s.Name == "中廣音樂網" && s.Format == StreamFormat.Aac), "New explorer BCC alias finds real 中廣音樂網 AAC");
            var query = new CatalogQuery { Country = "" };
            var first = await browser.BrowseAsync(query, null, cancel.Token);
            var second = await browser.BrowseAsync(query, first.Cursor, cancel.Token);
            var third = await browser.BrowseAsync(query, second.Cursor, cancel.Token);
            Assert(third.Cursor.Offsets[0] == 150 && third.Stations.Length > 0, "New global explorer continues to the third real API page");
            Assert(third.Stations.Any(s => !first.Stations.Concat(second.Stations).Any(old => old.Id == s.Id)), "Real third page contains additional stations beyond the first 100 candidates");
        }
    }

    private static async Task<bool> DecodeStation(RadioStation station, CancellationToken token)
    {
        var session = new LiveStreamSession(station.StreamUri, station.Format);
        try
        {
            Console.WriteLine("Checking stream: " + station.Name + " / " + station.Codec);
            DateTime deadline = DateTime.UtcNow.AddSeconds(25);
            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                var pcm = session.Output;
                if (pcm != null && pcm.Available >= pcm.SampleRate * pcm.Channels * 2)
                {
                    var audio = new float[pcm.SampleRate * pcm.Channels];
                    int count = pcm.Read(audio);
                    double energy = audio.Take(count).Sum(s => (double)s * s);
                    Assert(count > 0 && energy > 0 && audio.All(s => !float.IsNaN(s) && !float.IsInfinity(s)),
                        station.Codec + " decodes to non-silent finite PCM under .NET Framework 4.8");
                    Console.WriteLine("Format: " + pcm.SampleRate + " Hz / " + pcm.Channels + " channels; samples=" + count);
                    return true;
                }
                if (session.State == StreamState.Failed) break;
                await Task.Delay(100, token);
            }
            Console.WriteLine("Station unavailable: " + session.State + " / " + session.Error);
            return false;
        }
        finally
        {
            session.Dispose();
            Assert(await Task.WhenAny(session.Completion, Task.Delay(5000)) == session.Completion,
                "Stopping " + station.Codec + " releases the live connection within five seconds");
            await session.Completion;
        }
    }

    private static async Task CheckStalledDecoder()
    {
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var connection = server.AcceptTcpClientAsync();
        var session = new LiveStreamSession(new Uri("http://127.0.0.1:" + ((IPEndPoint)server.LocalEndpoint).Port + "/live.ogg"), StreamFormat.Ogg);
        try
        {
            Assert(await Task.WhenAny(connection, Task.Delay(10000)) == connection, "Decoder connects to a deliberately stalled HTTP server");
            using (await connection)
            {
                session.Dispose();
                Assert(await Task.WhenAny(session.Completion, Task.Delay(5000)) == session.Completion,
                    "Canceling during HTTP connection setup kills the decoder within five seconds");
                await session.Completion;
                Assert(session.State == StreamState.Stopped && session.Output == null, "Canceled decoder cannot publish late audio");
            }
        }
        finally { session.Dispose(); server.Stop(); }
    }

    private sealed class FragmentedStream : MemoryStream
    {
        public FragmentedStream(byte[] content) : base(content) { }
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(1, count));
    }
}
