using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LiveRadio.Core;
using NLayer;

internal static class Mp3RestartChecks
{
    internal static async Task RunFormatChange(string introFile, string mainFile, Action<bool, string> assert)
    {
        byte[] compressed = File.ReadAllBytes(introFile).Concat(File.ReadAllBytes(mainFile)).ToArray();
        int sampleRate, channels;
        using (var reference = new MpegFile(mainFile)) { sampleRate = reference.SampleRate; channels = reference.Channels; }
        using (var server = new BurstServer(compressed))
        using (var session = new LiveStreamSession(server.Url, StreamFormat.Mp3, "C:/intentionally-missing-ffmpeg.exe"))
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            PcmBuffer output;
            do
            {
                await Task.Delay(10); output = session.Output;
            } while (DateTime.UtcNow < deadline && (output == null || output.SampleRate != sampleRate || output.Channels != channels));
            Console.WriteLine($"MP3 format transition: rate={output?.SampleRate}; channels={output?.Channels}; state={session.State}; error={session.Error}.");
            assert(output != null && output.SampleRate == sampleRate && output.Channels == channels, "A short intro cannot fix the subsequent broadcast to the wrong playback format");
            var block = new float[output.SampleRate * output.Channels];
            while (DateTime.UtcNow < deadline && output.Available < block.Length * 2) await Task.Delay(10);
            assert(output.Read(block) == block.Length && block.All(s => !float.IsNaN(s) && !float.IsInfinity(s)) && block.Any(s => Math.Abs(s) > 0.01), "Changed-format MP3 produces finite audible PCM without FFmpeg");
            assert(output.DroppedSamples == 0 && output.Contentions == 0, "A format change does not flood or corrupt the replacement PCM queue");
            session.Dispose();
            assert(await Task.WhenAny(session.Completion, Task.Delay(3000)) == session.Completion, "A changed-format stream can still be canceled promptly");
        }
    }

    internal static async Task Run(string fixture, Action<bool, string> assert)
    {
        byte[] compressed = File.ReadAllBytes(fixture);
        float[] expected;
        using (var input = new IcyAudioStream(new MemoryStream(compressed), 0))
        using (var decoder = new MpegFile(input))
        {
            expected = new float[decoder.SampleRate * decoder.Channels];
            assert(decoder.ReadSamples(expected, 0, expected.Length) == expected.Length, "MP3 fixture contains the first second of reference PCM");
        }
        using (var server = new BurstServer(compressed))
        {
            LiveStreamSession session = null;
            var retired = new List<Task>();
            try
            {
                for (int cycle = 0; cycle < 4; cycle++)
                {
                    // Resume immediately: the previous network task may still be unwinding.
                    session = new LiveStreamSession(server.Url, StreamFormat.Mp3, "C:/intentionally-missing-ffmpeg.exe");
                    DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                    while (DateTime.UtcNow < deadline && (session.Output == null || session.Output.Available < expected.Length * 9.9)) await Task.Delay(10);
                    var output = session.Output;
                    assert(output != null && output.Available >= expected.Length * 9.9, "MP3 restart " + cycle + " fills the PCM buffer without FFmpeg");
                    await Task.Delay(150); // Let the initial HTTP burst reach a full queue.
                    var actual = new float[expected.Length];
                    int read = output.Read(actual);
                    Console.WriteLine($"MP3 restart {cycle}: read={read}; dropped={output.DroppedSamples}; lockMisses={output.Contentions}.");
                    assert(output.DroppedSamples == 0 && read == actual.Length && actual.SequenceEqual(expected), "MP3 restart " + cycle + " preserves the first second instead of skipping queued audio");
                    session.Dispose(); retired.Add(session.Completion); session = null;
                }
                var complete = Task.WhenAll(retired);
                assert(await Task.WhenAny(complete, Task.Delay(3000)) == complete, "Rapid MP3 pause/resume releases all previous network and decoder workers");
            }
            finally
            {
                session?.Dispose();
                if (session != null) retired.Add(session.Completion);
                await Task.WhenAny(Task.WhenAll(retired), Task.Delay(3000));
            }
        }
    }

    private sealed class BurstServer : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly List<Task> _clients = new List<Task>();
        private readonly Task _accept;
        public Uri Url { get; }
        public BurstServer(byte[] compressed)
        {
            _listener.Start();
            Url = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/live.mp3");
            _accept = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        lock (_clients) _clients.Add(Task.Run(async () =>
                        {
                            using (client)
                            try
                            {
                                var stream = client.GetStream();
                                byte[] one = new byte[1]; string tail = "";
                                while (await stream.ReadAsync(one, 0, 1) > 0)
                                {
                                    tail = (tail + (char)one[0]); if (tail.Length > 4) tail = tail.Substring(1);
                                    if (tail == "\r\n\r\n") break;
                                }
                                byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: audio/mpeg\r\nConnection: close\r\n\r\n");
                                await stream.WriteAsync(headers, 0, headers.Length);
                                await stream.WriteAsync(compressed, 0, compressed.Length);
                                await stream.WriteAsync(compressed, 0, compressed.Length);
                                // Remain live until the client pauses; no artificial EOF/reconnect.
                                await stream.ReadAsync(one, 0, 1);
                            }
                            catch (IOException) { } catch (SocketException) { }
                        }));
                    }
                }
                catch (ObjectDisposedException) { } catch (SocketException) { }
            });
        }
        public void Dispose()
        {
            _listener.Stop(); _accept.Wait(1000);
            lock (_clients) Task.WaitAll(_clients.ToArray(), 3000);
        }
    }
}
