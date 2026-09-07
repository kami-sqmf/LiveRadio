using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRadio.Core
{
    public enum StreamState { Connecting, Receiving, Reconnecting, Failed, Stopped }

    public sealed class LiveStreamSession : IDisposable
    {
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private int _disposed;
        private volatile PcmBuffer _output;
        private volatile StreamState _state = StreamState.Connecting;
        private volatile string _title = "";
        private volatile string _error = "";
        public PcmBuffer Output => _output;
        public StreamState State => _state;
        public string Title => _title;
        public string Error => _error;
        public Task Completion { get; }

        public LiveStreamSession(Uri uri, StreamFormat format = StreamFormat.Mp3, string ffmpegPath = null)
        {
            if (uri == null || !uri.IsAbsoluteUri || (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("HTTP(S) stream without embedded credentials required.");
            if (format == StreamFormat.Unsupported) throw new NotSupportedException(L10n.T("This stream format is not supported.", "尚未支援此串流格式。"));
            Completion = Task.Run(() => Receive(uri, format, ffmpegPath));
        }

        private void Receive(Uri uri, StreamFormat format, string ffmpegPath)
        {
            var token = _cancel.Token;
            int failures = 0;
            try
            {
                while (!token.IsCancellationRequested && failures < 5)
                {
                    DateTime started = DateTime.UtcNow;
                    try
                    {
                        _state = failures == 0 ? StreamState.Connecting : StreamState.Reconnecting;
                        if (format != StreamFormat.Mp3)
                        {
                            FfmpegDecoder.Receive(uri, format, FfmpegDecoder.FindExecutable(ffmpegPath), token, output =>
                            {
                                _output = output;
                                _error = "";
                                _state = StreamState.Receiving;
                            });
                            throw new EndOfStreamException(L10n.T("The station ended the stream.", "電台已中斷串流。"));
                        }
#pragma warning disable SYSLIB0014
                        var request = (HttpWebRequest)WebRequest.Create(uri);
#pragma warning restore SYSLIB0014
                        request.UserAgent = RadioBrowserClient.UserAgent;
                        request.Timeout = 15000;
                        request.ReadWriteTimeout = 15000;
                        request.MaximumAutomaticRedirections = 5;
                        request.Headers["Icy-MetaData"] = "1";
                        using (token.Register(request.Abort))
                        using (var response = (HttpWebResponse)request.GetResponse())
                        {
                            string contentType = (response.ContentType ?? "").ToLowerInvariant();
                            if (contentType.Contains("mpegurl") || contentType.Contains("aac") || contentType.Contains("text/html"))
                                throw new NotSupportedException(L10n.T("This is not a direct MP3 stream. Use Auto or the correct stream format.", "此串流不是直接 MP3 音訊。"));
                            int.TryParse(response.Headers["icy-metaint"], out int interval);
                            if (interval < 0 || interval > 16 * 1024 * 1024) throw new IOException("Invalid ICY interval.");
                            using (var stream = new IcyAudioStream(response.GetResponseStream(), interval, title => _title = title))
                            {
                                Mp3Decoder.Receive(stream, token, output =>
                                {
                                    _output = output;
                                    _error = "";
                                    _state = StreamState.Receiving;
                                });
                                throw new EndOfStreamException(L10n.T("The station ended the stream.", "電台已中斷串流。"));
                            }
                        }
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        _output = null;
                        _error = ex.Message;
                        if (ex is NotSupportedException || ex is System.ComponentModel.Win32Exception) { _state = StreamState.Failed; return; }
                        failures = (DateTime.UtcNow - started).TotalSeconds >= 30 ? 1 : failures + 1;
                        if (failures >= 5) { _state = StreamState.Failed; return; }
                        _state = StreamState.Reconnecting;
                        if (token.WaitHandle.WaitOne(TimeSpan.FromSeconds(Math.Min(30, 1 << failures)))) break;
                    }
                }
            }
            catch (Exception) when (token.IsCancellationRequested) { }
            finally
            {
                if (token.IsCancellationRequested) { _output = null; _state = StreamState.Stopped; }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _cancel.Cancel();
            // Do not block Unity's main thread on a slow server; Abort interrupts the active request.
            Completion.ContinueWith(_ => _cancel.Dispose(), TaskScheduler.Default);
        }
    }
}
