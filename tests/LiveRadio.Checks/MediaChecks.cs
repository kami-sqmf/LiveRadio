using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveRadio.Core;

internal static class MediaChecks
{
    internal static async Task Run(string directory, Action<bool, string> assert)
    {
        using (var server = new FixtureServer(directory))
        {
            foreach (var example in new[] { ("media/live.m3u8", StreamFormat.Hls), ("master.m3u8", StreamFormat.Hls),
                ("fmp4/live.m3u8", StreamFormat.Hls), ("encrypted/live.m3u8", StreamFormat.Hls), ("media/live.m3u8", StreamFormat.Auto), ("direct.mp3", StreamFormat.Mp3) })
            {
                var session = new LiveStreamSession(new Uri(server.Url + example.Item1), example.Item2, example.Item2 == StreamFormat.Mp3 ? "C:/intentionally-missing.exe" : null);
                try
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(20); bool played = false;
                    while (DateTime.UtcNow < deadline)
                    {
                        var output = session.Output;
                        if (output != null && output.Available > output.SampleRate * output.Channels * 2)
                        {
                            var audio = new float[output.SampleRate * output.Channels];
                            int read = output.Read(audio);
                            if (read == 0) { await Task.Delay(50); continue; }
                            played = read > 0 && audio.Take(read).Sum(v => (double)v * v) > 0.01 && audio.All(v => !float.IsNaN(v) && !float.IsInfinity(v));
                            break;
                        }
                        if (session.State == StreamState.Failed) break;
                        await Task.Delay(50);
                    }
                    assert(played, example.Item1 + " (" + example.Item2 + ") produces finite non-silent PCM: " + session.Error);
                }
                finally
                {
                    session.Dispose();
                    assert(await Task.WhenAny(session.Completion, Task.Delay(5000)) == session.Completion && session.Output == null, "Stopping " + example.Item1 + " releases the decoder and buffer within 5 seconds");
                }
            }
            string pngPath = Path.Combine(directory, "fetched-icon");
            string result = LiveRadio.StationArtwork.Download(server.Url + "icon.png", pngPath, CancellationToken.None);
            using (var png = new System.Drawing.Bitmap(result)) assert(png.Width == 96 && png.Height == 96, "Remote station artwork is validated and cached for the native panel");
            string iconCache = Path.Combine(directory, "artwork-cache-" + Guid.NewGuid().ToString("N"));
            var remoteIcon = new RadioStation { Id = Guid.NewGuid().ToString(), Name = "HTTP artwork", Favicon = server.Url + "icon.png" };
            using (var artwork = new LiveRadio.StationArtwork(iconCache, null))
            {
                assert(artwork.Get(remoteIcon, false) == "" && !Directory.GetFiles(iconCache).Any(), "Uncached explorer artwork uses text without exposing an HTTP URL or drawing images on the UI thread");
                artwork.Open(Array.Empty<RadioStation>());
                artwork.Get(remoteIcon, false);
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                string localIcon = "";
                while (DateTime.UtcNow < deadline && localIcon.Length == 0)
                {
                    await Task.Delay(20); artwork.Tick(); localIcon = artwork.Get(remoteIcon, false);
                }
                assert(localIcon == "coui://liveradio-icons/" + Guid.Parse(remoteIcon.Id).ToString("N") + ".png", "HTTP artwork is downloaded in the worker and exposed only through the local resource host");
                assert(artwork.Get(remoteIcon) == localIcon, "Favoriting a downloaded explorer result reuses its cached logo");
                var missing = new RadioStation { Id = Guid.NewGuid().ToString(), Name = "Missing icon", Favicon = server.Url + "missing.png" };
                assert(artwork.Get(missing, false) == "", "A failed or pending icon never exposes its remote address");
                await Task.Delay(100); artwork.Tick();
                assert(artwork.Get(missing).EndsWith("-initial.png"), "Favoriting a station with no cached logo creates a native initial icon");
            }
            using (var reopened = new LiveRadio.StationArtwork(iconCache, null))
                assert(reopened.Get(remoteIcon, false).EndsWith(Guid.Parse(remoteIcon.Id).ToString("N") + ".png"), "Downloaded artwork is reused after reopening the cache");
            // A moving playlist publishes ~10-second segments in bursts with download jitter.
            // The old overwrite queue both skipped audio and starved at these boundaries.
            await ContinuityChecks.Run(new Uri(server.Url + "paced.m3u8"), StreamFormat.Hls, 35, true, assert);
            // A running game can switch stations and start its own decoder during these tests.
            using (var process = Process.GetCurrentProcess())
            using (var children = new ManagementObjectSearcher("SELECT ProcessId FROM Win32_Process WHERE Name = 'ffmpeg.exe' AND ParentProcessId = " + process.Id))
            using (var remaining = children.Get())
                assert(remaining.Count == 0, "Media checks leave no FFmpeg child process behind");
            assert(server.Requests.Any(p => p.EndsWith(".ts")) && server.Requests.Any(p => p.EndsWith(".m4s")) && server.Requests.Contains("/encrypted/key.bin"), "HLS follows TS segments, fragmented MP4 segments and AES keys over HTTP");
        }
    }
    private sealed class FixtureServer : IDisposable
    {
        private readonly TcpListener _server = new TcpListener(IPAddress.Loopback, 0);
        private readonly Dictionary<string, byte[]> _files;
        private readonly List<Task> _clients = new List<Task>();
        private readonly Task _accept;
        private readonly string[] _segmentLines;
        private Stopwatch _playlistClock;
        public readonly System.Collections.Concurrent.ConcurrentBag<string> Requests = new System.Collections.Concurrent.ConcurrentBag<string>();
        public string Url { get; }
        public FixtureServer(string directory)
        {
            _files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(p => "/" + p.Substring(directory.Length).TrimStart('\\', '/').Replace('\\', '/'), File.ReadAllBytes);
            _segmentLines = Encoding.UTF8.GetString(_files["/media/live.m3u8"]).Split('\n')
                .Select(s => s.Trim()).Where(s => s.StartsWith("#EXTINF:") || s.EndsWith(".ts")).ToArray();
            _server.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)_server.LocalEndpoint).Port + "/";
            _accept = Task.Run(async () =>
            {
                try { while (true) { var client = await _server.AcceptTcpClientAsync(); lock (_clients) _clients.Add(Task.Run(() => Serve(client))); } }
                catch (ObjectDisposedException) { } catch (SocketException) { }
            });
        }
        private async Task Serve(TcpClient client)
        {
            using (client)
            try
            {
                var stream = client.GetStream();
                var header = new List<byte>(); var one = new byte[1];
                while (header.Count < 16384 && await stream.ReadAsync(one, 0, 1) > 0)
                {
                    header.Add(one[0]);
                    if (header.Count >= 4 && Encoding.ASCII.GetString(header.Skip(header.Count - 4).ToArray()) == "\r\n\r\n") break;
                }
                string path = Encoding.ASCII.GetString(header.ToArray()).Split(' ')[1].Split('?')[0]; Requests.Add(path);
                byte[] content;
                bool found;
                if (path == "/paced.m3u8")
                {
                    lock (_segmentLines)
                    {
                        if (_playlistClock == null) _playlistClock = Stopwatch.StartNew();
                        int end = Math.Min(_segmentLines.Length / 2, 3 + (int)(_playlistClock.Elapsed.TotalSeconds / 10));
                        int start = Math.Max(0, end - 3);
                        var playlist = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:11\n#EXT-X-MEDIA-SEQUENCE:" + start + "\n");
                        for (int n = start; n < end; n++) playlist.Append(_segmentLines[n * 2]).Append('\n').Append("paced/").Append(_segmentLines[n * 2 + 1]).Append('\n');
                        content = Encoding.UTF8.GetBytes(playlist.ToString()); found = true;
                    }
                }
                else
                {
                    string filePath = path.StartsWith("/paced/") ? path.Replace("/paced/", "/media/") : path;
                    found = _files.TryGetValue(filePath, out content);
                    if (!found) content = Array.Empty<byte>();
                    if (path.StartsWith("/paced/")) await Task.Delay(path.EndsWith("3.ts") ? 1800 : 150);
                }
                string mime = path.EndsWith(".m3u8") ? "application/vnd.apple.mpegurl" : path.EndsWith(".png") ? "image/png" : "application/octet-stream";
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 " + (found ? "200 OK" : "404 Not Found") + "\r\nContent-Length: " + content.Length + "\r\nContent-Type: " + mime + "\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, 0, response.Length);
                for (int offset = 0; offset < content.Length; offset += 4096)
                {
                    await stream.WriteAsync(content, offset, Math.Min(4096, content.Length - offset));
                    // A direct MP3 endpoint remains a live connection long enough to observe its PCM.
                    if (path.EndsWith(".mp3")) await Task.Delay(20);
                }
            }
            catch (IOException) { } catch (SocketException) { }
        }
        public void Dispose() { _server.Stop(); _accept.Wait(1000); lock (_clients) Task.WaitAll(_clients.ToArray(), 3000); }
    }
}
