using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AirplayRadio
{
    internal enum RemoteCommand { Previous, Next, Pause, Play }
    internal sealed class RemoteResult
    {
        internal RemoteCommand Command;
        internal int Code;
        internal bool Success => Code == 200 || Code == 204;
    }
    internal sealed class DacpReply
    {
        internal int Code;
        internal byte[] Body = Array.Empty<byte>();
    }
    internal sealed class DacpRemote : IDisposable
    {
        private readonly RemoteIdentity _identity;
        private readonly Func<RemoteIdentity, CancellationToken, int> _discover;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly ConcurrentQueue<RemoteCommand> _commands = new ConcurrentQueue<RemoteCommand>();
        private readonly ConcurrentQueue<RemoteResult> _results = new ConcurrentQueue<RemoteResult>();
        private readonly Task _worker;
        private int _disposed, _busy, _port, _paused = -1;
        private volatile LocalizedText _status = L10n.Message("Finding phone controls", "正在尋找手機遙控");
        internal bool Ready => Volatile.Read(ref _port) != 0 && Volatile.Read(ref _disposed) == 0;
        internal LocalizedText Status => _status;
        internal bool? Paused { get { int n = Volatile.Read(ref _paused); return n < 0 ? (bool?)null : n != 0; } }
        internal Task Completion => _worker;
        internal DacpRemote(RemoteIdentity identity, Func<RemoteIdentity, CancellationToken, int> discover = null)
        {
            _identity = identity;
            _discover = discover ?? Discover;
            _worker = Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        internal bool Send(RemoteCommand command)
        {
            if (!Ready || Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return false;
            _commands.Enqueue(command); return true;
        }
        internal bool TryResult(out RemoteResult result) => _results.TryDequeue(out result);
        private void Run()
        {
            var token = _stop.Token;
            try
            {
                long nextDiscovery = 0, nextPoll = 0;
                var clock = Stopwatch.StartNew();
                bool pollSupported = true;
                while (!token.IsCancellationRequested)
                {
                    if (!Ready && clock.ElapsedMilliseconds >= nextDiscovery)
                    {
                        int port = _discover(_identity, token);
                        nextDiscovery = clock.ElapsedMilliseconds + 10000;
                        if (port != 0)
                        {
                            // A harmless query checks reachability. Basic clients may reject properties.
                            var probe = Request(_identity, port, "getproperty?properties=dmcp.volume", token);
                            if (probe.Code == 200 || probe.Code == 204 || probe.Code == 400 || probe.Code == 404)
                            {
                                Volatile.Write(ref _paused, -1); Volatile.Write(ref _port, port); pollSupported = true;
                                _status = L10n.Message("Phone controls connected", "手機遙控已連線");
                            }
                            else _status = L10n.Message("Phone controls unavailable", "手機遙控無法連線");
                        }
                        else _status = L10n.Message("No phone control service", "手機未提供遙控服務");
                    }
                    if (_commands.TryDequeue(out var command))
                    {
                        var reply = Request(_identity, Volatile.Read(ref _port), CommandPath(command), token);
                        token.ThrowIfCancellationRequested();
                        var result = new RemoteResult { Command = command, Code = reply.Code };
                        if (result.Success)
                        {
                            if (command == RemoteCommand.Pause || command == RemoteCommand.Play)
                                Volatile.Write(ref _paused, command == RemoteCommand.Pause ? 1 : 0);
                            _status = L10n.Message("Phone accepted control", "手機已接受控制");
                        }
                        else
                        {
                            _status = reply.Code == 0 ? L10n.Message("Phone control timed out", "手機遙控逾時") : L10n.Message("Phone rejected control (HTTP {0})", "手機不接受此控制（HTTP {0}）", reply.Code);
                            if (reply.Code == 0 || reply.Code == 401 || reply.Code == 403) Volatile.Write(ref _port, 0);
                        }
                        _results.Enqueue(result); Interlocked.Exchange(ref _busy, 0);
                        nextPoll = clock.ElapsedMilliseconds + 500;
                    }
                    if (Ready && pollSupported && clock.ElapsedMilliseconds >= nextPoll)
                    {
                        var reply = Request(_identity, Volatile.Read(ref _port), "playstatusupdate?revision-number=1", token);
                        token.ThrowIfCancellationRequested();
                        var paused = RemoteProtocol.Paused(reply.Body);
                        if (reply.Code == 200 && paused.HasValue) Volatile.Write(ref _paused, paused.Value ? 1 : 0);
                        else { pollSupported = false; _status = L10n.Message("Phone controls ready; status polling unavailable", "遙控可用，無播放狀態回報"); }
                        nextPoll = clock.ElapsedMilliseconds + 2000;
                    }
                    token.WaitHandle.WaitOne(100);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { _status = L10n.Message("Phone control stopped", "手機遙控已停止"); }
            finally { Volatile.Write(ref _port, 0); Interlocked.Exchange(ref _busy, 0); }
        }
        internal static string CommandPath(RemoteCommand c)
        {
            switch (c) {
                case RemoteCommand.Previous: return "previtem";
                case RemoteCommand.Next: return "nextitem";
                case RemoteCommand.Pause: return "pause";
                case RemoteCommand.Play: return "play";
                default: throw new ArgumentOutOfRangeException(nameof(c));
            }
        }
        internal static DacpReply Request(RemoteIdentity identity, int port, string command, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (port < 1 || port > 65535) return new DacpReply();
            var address = new Uri(new UriBuilder("http", identity.Peer.ToString(), port).Uri, "ctrl-int/1/" + command);
            var request = (HttpWebRequest)WebRequest.Create(address);
            request.Proxy = null; request.AllowAutoRedirect = false; request.KeepAlive = false;
            request.Timeout = 1500; request.ReadWriteTimeout = 1000; request.MaximumResponseHeadersLength = 16;
            request.Headers["Active-Remote"] = identity.Token;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (deadline.Token.Register(request.Abort))
            {
                deadline.CancelAfter(2500);
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    using (var body = new MemoryStream())
                    {
                        byte[] buffer = new byte[2048]; int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (body.Length + read > 65536) return new DacpReply();
                            body.Write(buffer, 0, read);
                        }
                        return new DacpReply { Code = (int)response.StatusCode, Body = body.ToArray() };
                    }
                }
                catch (WebException ex)
                {
                    cancellation.ThrowIfCancellationRequested();
                    using (var response = ex.Response as HttpWebResponse)
                        return new DacpReply { Code = response == null ? 0 : (int)response.StatusCode };
                }
                catch (IOException) { cancellation.ThrowIfCancellationRequested(); return new DacpReply(); }
            }
        }
        internal static int Discover(RemoteIdentity identity, CancellationToken cancellation)
        {
            var sockets = new System.Collections.Generic.List<UdpClient>();
            try
            {
                var locals = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address)
                    .Where(a => a.AddressFamily == identity.Peer.AddressFamily && !IPAddress.IsLoopback(a)).Take(16).ToArray();
                foreach (var address in locals)
                {
                    if (address.IsIPv6LinkLocal && identity.Peer.ScopeId != 0 && address.ScopeId != identity.Peer.ScopeId) continue;
                    try { sockets.Add(new UdpClient(new IPEndPoint(address, 0))); } catch (SocketException) { }
                }
                if (sockets.Count == 0) return 0;
                void Query(byte[] query)
                {
                    foreach (var socket in sockets)
                    {
                        try
                        {
                            socket.Send(query, query.Length, new IPEndPoint(identity.Peer, 5353));
                            var local = (IPEndPoint)socket.Client.LocalEndPoint;
                            IPAddress group = local.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Parse("224.0.0.251") : new IPAddress(IPAddress.Parse("ff02::fb").GetAddressBytes(), local.Address.ScopeId);
                            if (local.AddressFamily == AddressFamily.InterNetwork)
                                socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.Address.GetAddressBytes());
                            socket.Send(query, query.Length, new IPEndPoint(group, 5353));
                        }
                        catch (SocketException) { }
                    }
                }
                Query(RemoteProtocol.Query("_dacp._tcp.local", true));
                Query(RemoteProtocol.Query("iTunes_Ctrl_" + identity.Id.ToString("X") + "._dacp._tcp.local"));
                var clock = Stopwatch.StartNew(); int followups = 0;
                while (clock.ElapsedMilliseconds < 2000)
                {
                    cancellation.ThrowIfCancellationRequested();
                    foreach (var socket in sockets)
                    {
                        if (socket.Available == 0) continue;
                        IPEndPoint source = null; byte[] packet = socket.Receive(ref source);
                        // Never follow a DNS record to a different host than the connected RTSP peer.
                        if (source.Port != 5353 || !source.Address.GetAddressBytes().SequenceEqual(identity.Peer.GetAddressBytes())) continue;
                        int port = RemoteProtocol.FindPort(packet, identity.Id, out string service);
                        if (port != 0) return port;
                        if (service != null && followups++ < 3) Query(RemoteProtocol.Query(service));
                    }
                    cancellation.WaitHandle.WaitOne(20);
                }
            }
            catch (NetworkInformationException) { }
            catch (SocketException) { }
            finally { foreach (var socket in sockets) socket.Dispose(); }
            return 0;
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _stop.Cancel();
            _worker.ContinueWith(_ => _stop.Dispose(), TaskScheduler.Default);
        }
    }
}
