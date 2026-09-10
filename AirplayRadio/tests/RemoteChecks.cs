using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirplayRadio;

internal static class RemoteChecks
{
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void Eventually(Func<bool> check, string why, int ms = 5000) { Check(SpinWait.SpinUntil(check, ms), why); }
    internal static void Run()
    {
        Check(RemoteIdentity.Parse("1\n127.0.0.1\nABCD\n1234") != null, "Valid remote identity");
        Check(RemoteIdentity.Parse("1\n127.0.0.1\nABCD\n1234\rInjected: yes") == null, "Reject header injection");
        Check(RemoteIdentity.Parse("1\n224.0.0.251\nABCD\n1234") == null, "Reject multicast control host");
        Check(RemoteIdentity.Parse("1\n::ffff:192.168.1.10\nABCD\n1234").Peer.AddressFamily == AddressFamily.InterNetwork, "Normalize IPv4-mapped peer for discovery");
        var identity = RemoteIdentity.Parse("1\n127.0.0.1\nABCD\n1234");
        var dns = new List<byte>(RemoteProtocol.Query("iTunes_Ctrl_000000000000ABCD._dacp._tcp.local"));
        dns[2] = 0x84; dns[7] = 1;
        var target = new List<byte> { 0, 0, 0, 0, 125, 0 };
        target.AddRange(new byte[] { 5, (byte)'p', (byte)'h', (byte)'o', (byte)'n', (byte)'e', 5, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', 0 });
        dns.AddRange(new byte[] { 192, 12, 0, 33, 128, 1, 0, 0, 0, 120, 0, (byte)target.Count });
        dns.AddRange(target);
        Check(RemoteProtocol.FindPort(dns.ToArray(), 0xABCD, out _) == 32000, "Compressed SRV response resolves matching ID");
        Check(RemoteProtocol.FindPort(dns.ToArray(), 0xBEEF, out _) == 0, "Ignore another sender ID");
        Check(RemoteProtocol.FindPort(dns.Take(dns.Count - 2).ToArray(), 0xABCD, out _) == 0, "Reject truncated DNS record");
        var loop = dns.ToArray(); loop[12] = 192; loop[13] = 12;
        Check(RemoteProtocol.FindPort(loop, 0xABCD, out _) == 0, "Reject cyclic DNS compression");
        byte[] paused = { (byte)'c', (byte)'m', (byte)'s', (byte)'t', 0, 0, 0, 9, (byte)'c', (byte)'a', (byte)'p', (byte)'s', 0, 0, 0, 1, 3 };
        Check(RemoteProtocol.Paused(paused) == true, "Parse phone paused status");
        paused[16] = 4; Check(RemoteProtocol.Paused(paused) == false, "Parse phone playing status");
        paused[7] = 100; Check(RemoteProtocol.Paused(paused) == null, "Reject truncated status");

        using (var server = new FakePhone())
        {
            var reply = DacpRemote.Request(identity, server.Port, "redirect", CancellationToken.None);
            Check(reply.Code == 302 && server.RedirectTargetCalls == 0, "Do not follow phone HTTP redirects");
            var remote = new DacpRemote(identity, (_, token) => server.Port);
            try
            {
                Eventually(() => remote.Ready, "Remote discovery/probe makes controls available");
                Check(remote.Send(RemoteCommand.Next), "Next command enqueued");
                RemoteResult result = null;
                Eventually(() => remote.TryResult(out result), "Next command completes");
                Check(result.Success && server.NextCalls == 1, "Next command delivered once");
                Check(remote.Send(RemoteCommand.Pause), "Pause command enqueued");
                Eventually(() => remote.TryResult(out result), "Pause command completes");
                Check(result.Success && remote.Paused == true, "Pause acknowledged");
                Check(remote.Send(RemoteCommand.Play), "Play command enqueued");
                Eventually(() => remote.TryResult(out result), "Play command completes");
                Check(result.Success && remote.Paused == false, "Play acknowledged");
                Check(remote.Send(RemoteCommand.Previous), "Previous command enqueued");
                Eventually(() => remote.TryResult(out result), "Previous command completes");
                Check(!result.Success && result.Code == 405, "Rejected control is not reported successful");
                server.BlockNext = true;
                Check(remote.Send(RemoteCommand.Next), "Blocking control enqueued");
                Check(server.Blocked.Wait(5000), "Phone received blocking control");
                remote.Dispose();
                Check(remote.Completion.Wait(3000), "Disconnect cancels in-flight HTTP promptly");
                Check(!remote.Send(RemoteCommand.Play), "Disposed session rejects new commands");
                Check(server.NextCalls == 2, "Timed out/cancelled next is never retried");
                Check(server.BadTokens == 0 && server.RedirectTargetCalls == 0, "All commands authenticated without redirection");
            }
            finally { remote.Dispose(); server.Release.Set(); }
        }
        Console.WriteLine("PASS: remote identity isolation, DNS compression/bounds, play-state parsing, HTTP commands/auth, rejection, no redirects/retries, cancellation");
    }

    private sealed class FakePhone : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly Task _worker;
        internal readonly ManualResetEventSlim Blocked = new ManualResetEventSlim();
        internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        internal volatile bool BlockNext;
        internal int NextCalls, BadTokens, RedirectTargetCalls;
        private bool _paused;
        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        internal FakePhone()
        {
            _listener.Start();
            _worker = Task.Run(() => {
                while (!_stop.IsCancellationRequested)
                {
                    try { using (var client = _listener.AcceptTcpClient()) Handle(client); }
                    catch (SocketException) { if (!_stop.IsCancellationRequested) throw; }
                    catch (IOException) { }
                }
            });
        }
        private void Handle(TcpClient client)
        {
            client.ReceiveTimeout = 2000;
            using (var stream = client.GetStream())
            {
                var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                string first = reader.ReadLine(), line; bool auth = false;
                while (!string.IsNullOrEmpty(line = reader.ReadLine())) if (line == "Active-Remote: 1234") auth = true;
                if (!auth) Interlocked.Increment(ref BadTokens);
                string path = first.Split(' ')[1]; int code = 204; byte[] body = Array.Empty<byte>(); string extra = "";
                if (path.EndsWith("/nextitem"))
                {
                    Interlocked.Increment(ref NextCalls);
                    if (BlockNext) { Blocked.Set(); Release.Wait(5000); }
                }
                else if (path.EndsWith("/previtem")) code = 405;
                else if (path.EndsWith("/pause")) _paused = true;
                else if (path.EndsWith("/play")) _paused = false;
                else if (path.Contains("playstatusupdate"))
                {
                    code = 200;
                    body = new byte[] { (byte)'c', (byte)'m', (byte)'s', (byte)'t', 0, 0, 0, 9, (byte)'c', (byte)'a', (byte)'p', (byte)'s', 0, 0, 0, 1, (byte)(_paused ? 3 : 4) };
                }
                else if (path.EndsWith("/redirect")) { code = 302; extra = "Location: http://127.0.0.1:" + Port + "/unexpected\r\n"; }
                else if (path.EndsWith("/unexpected")) Interlocked.Increment(ref RedirectTargetCalls);
                else code = 400; // basic DACP clients need not support volume/property queries
                byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " Test\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n" + extra + "\r\n");
                stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length);
            }
        }
        public void Dispose()
        {
            _stop.Cancel(); Release.Set(); _listener.Stop(); _worker.Wait(3000); _stop.Dispose();
            Blocked.Dispose(); Release.Dispose();
        }
    }
}
