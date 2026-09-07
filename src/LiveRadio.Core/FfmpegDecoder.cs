using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRadio.Core
{
    public static class FfmpegDecoder
    {
        public static string FindExecutable(string configuredPath = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string path = Environment.ExpandEnvironmentVariables(configuredPath.Trim().Trim('"'));
                return ExistingExecutable(path);
            }
            var candidates = new List<string>();
            // Resolve Chocolatey's real binary before PATH: killing a shim can leave its child running.
            string chocolatey = Environment.GetEnvironmentVariable("ChocolateyInstall");
            if (string.IsNullOrEmpty(chocolatey)) chocolatey = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey");
            candidates.Add(Path.Combine(chocolatey, "lib", "ffmpeg", "tools", "ffmpeg", "bin", "ffmpeg.exe"));
            foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                string directory = entry.Trim().Trim('"');
                try { if (Path.IsPathRooted(directory)) candidates.Add(Path.Combine(directory, "ffmpeg.exe")); }
                catch (ArgumentException) { }
            }
            foreach (string candidate in candidates)
            {
                string executable = ExistingExecutable(candidate);
                if (executable != null) return executable;
            }
            return null;
        }

        private static string ExistingExecutable(string path)
        {
            try
            {
                if (!Path.IsPathRooted(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
                if ((FileVersionInfo.GetVersionInfo(path).ProductName ?? "").IndexOf("ShimGen", StringComparison.OrdinalIgnoreCase) >= 0) return null;
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException || ex is System.ComponentModel.Win32Exception || ex is NotSupportedException) { return null; }
        }

        internal static void Receive(Uri uri, StreamFormat format, string executable, CancellationToken token, Action<PcmBuffer> publish)
        {
            if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
                throw new NotSupportedException(L10n.T("This stream needs FFmpeg. Set the full path to ffmpeg.exe in Options > Live Radio > Audio decoder.", "此串流需要 FFmpeg；請在 Live Radio 音訊解碼器設定填入 ffmpeg.exe 完整路徑。"));
            var info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = BuildArguments(uri, format),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            token.ThrowIfCancellationRequested();
            using (var process = new Process { StartInfo = info })
            {
                process.Start();
                var errors = new StringBuilder();
                var errorTask = Task.Run(() =>
                {
                    try
                    {
                        var chars = new char[512];
                        int read;
                        while ((read = process.StandardError.Read(chars, 0, chars.Length)) > 0)
                            lock (errors)
                            {
                                errors.Append(chars, 0, read);
                                if (errors.Length > 2048) errors.Remove(0, errors.Length - 2048);
                            }
                    }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { }
                });
                long lastAudio = Stopwatch.GetTimestamp();
                bool segmented = format == StreamFormat.Hls || format == StreamFormat.Auto;
                var output = new PcmBuffer(48000, 2, segmented ? 20 : 10, segmented ? 6 : 2, lossless: true);
                // A stream that keeps sending headers/junk must not hold the decoder indefinitely.
                using (var watchdog = new Timer(_ =>
                {
                    // A full queue intentionally blocks the decoder. Only time out a
                    // starved stream, not a producer waiting for playback to catch up.
                    if (output.Available < output.SampleRate * output.Channels &&
                        (Stopwatch.GetTimestamp() - Interlocked.Read(ref lastAudio)) / (double)Stopwatch.Frequency > 25) Kill(process);
                }, null, 5000, 5000))
                using (token.Register(() => Kill(process)))
                {
                    try
                    {
                        bool published = false;
                        CopyPcm(process.StandardOutput.BaseStream, output, token, () =>
                        {
                            Interlocked.Exchange(ref lastAudio, Stopwatch.GetTimestamp());
                            if (!published) { publish(output); published = true; }
                        });
                        token.ThrowIfCancellationRequested();
                        string detail;
                        lock (errors) detail = errors.ToString().Trim();
                        throw new IOException(L10n.T("The decoder stopped.", "電台解碼已中止。") + (detail.Length == 0 ? "" : " " + detail));
                    }
                    finally
                    {
                        Kill(process);
                        process.WaitForExit(2000);
                        errorTask.Wait(1000);
                    }
                }
            }
        }

        internal static string BuildArguments(Uri uri, StreamFormat format)
        {
            // Nested HLS playlists/segments stay HTTP(S). No file, concat, data or input pipe protocol.
            // Keep FFmpeg's HLS extension checks; do not enable arbitrary formats for Auto URLs.
            string demuxer = format == StreamFormat.Aac ? " -f aac" : format == StreamFormat.Ogg ? " -f ogg" :
                format == StreamFormat.Hls ? " -f hls -live_start_index -3" : "";
            return "-nostdin -hide_banner -loglevel error -rw_timeout 15000000 " +
                "-protocol_whitelist http,https,tcp,tls,crypto -format_whitelist mp3,aac,ogg,hls,mpegts,mov " +
                "-user_agent " + QuoteArgument(RadioBrowserClient.UserAgent) + " -analyzeduration 1000000" + demuxer +
                " -i " + QuoteArgument(uri.AbsoluteUri) + " -map 0:a:0 -vn -sn -dn -threads 1 " +
                "-f f32le -acodec pcm_f32le -ar 48000 -ac 2 pipe:1";
        }

        internal static void CopyPcm(Stream stream, PcmBuffer output, CancellationToken token, Action received)
        {
            var bytes = new byte[16384];
            var samples = new float[bytes.Length / sizeof(float)];
            int remaining = 0, read;
            while ((read = stream.Read(bytes, remaining, bytes.Length - remaining)) > 0)
            {
                token.ThrowIfCancellationRequested();
                int total = remaining + read;
                int aligned = total - total % (sizeof(float) * output.Channels);
                if (aligned > 0)
                {
                    Buffer.BlockCopy(bytes, 0, samples, 0, aligned);
                    output.WriteBlocking(samples, aligned / sizeof(float), token);
                    received();
                }
                remaining = total - aligned;
                if (remaining > 0) Buffer.BlockCopy(bytes, aligned, bytes, 0, remaining);
            }
            if (remaining > 0 && !token.IsCancellationRequested) throw new EndOfStreamException("Incomplete PCM frame.");
        }

        private static void Kill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        internal static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', slashes * (c == '"' ? 2 : 1) + (c == '"' ? 1 : 0));
                result.Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
