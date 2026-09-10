using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace AirplayRadio
{
    internal sealed class RemoteIdentity
    {
        internal IPAddress Peer;
        internal ulong Id;
        internal string Token;
        internal static RemoteIdentity Parse(string snapshot)
        {
            var fields = snapshot.Split('\n');
            if (fields.Length != 4 || !ulong.TryParse(fields[0], out _) ||
                !IPAddress.TryParse(fields[1], out var peer) ||
                fields[2].Length < 1 || fields[2].Length > 16 ||
                !ulong.TryParse(fields[2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var id) ||
                fields[3].Length < 1 || fields[3].Length > 32) return null;
            foreach (char c in fields[3]) if (c < '0' || c > '9') return null;
            if (peer.IsIPv4MappedToIPv6) peer = peer.MapToIPv4();
            if (peer.Equals(IPAddress.Any) || peer.Equals(IPAddress.IPv6Any) || peer.IsIPv6Multicast ||
                (peer.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && peer.GetAddressBytes()[0] >= 224)) return null;
            return new RemoteIdentity { Peer = peer, Id = id, Token = fields[3] };
        }
    }

    // Bounded DNS parsing: compression pointers may be cyclic or outside the packet.
    internal static class RemoteProtocol
    {
        private static int U16(byte[] b, int p) => (b[p] << 8) | b[p + 1];
        private static uint U32(byte[] b, int p) => ((uint)b[p] << 24) | ((uint)b[p + 1] << 16) | ((uint)b[p + 2] << 8) | b[p + 3];
        private static string Name(byte[] b, ref int pos)
        {
            int cursor = pos, jumps = 0;
            bool jumped = false;
            var name = new StringBuilder();
            while (cursor < b.Length && ++jumps <= 128)
            {
                int n = b[cursor++];
                if (n == 0) { if (!jumped) pos = cursor; return name.ToString(); }
                if ((n & 192) == 192)
                {
                    if (cursor >= b.Length) break;
                    int target = ((n & 63) << 8) | b[cursor++];
                    if (!jumped) pos = cursor;
                    jumped = true; cursor = target; continue;
                }
                if (n > 63 || cursor + n > b.Length || name.Length + n + 1 > 255) break;
                if (name.Length > 0) name.Append('.');
                for (int i = 0; i < n; ++i)
                {
                    byte c = b[cursor++];
                    if (c < 33 || c > 126) throw new InvalidDataException("Invalid DNS label");
                    name.Append((char)c);
                }
                if (!jumped) pos = cursor;
            }
            throw new InvalidDataException("Invalid DNS name");
        }
        private static bool Matches(string name, ulong id)
        {
            const string prefix = "iTunes_Ctrl_", suffix = "._dacp._tcp.local";
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return false;
            string hex = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
            return hex.Length > 0 && hex.Length <= 16 && ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) && value == id;
        }
        internal static byte[] Query(string service, bool ptr = false)
        {
            var b = new List<byte>(new byte[12]); b[5] = 1;
            foreach (var label in service.Split('.'))
            {
                if (label.Length < 1 || label.Length > 63) throw new ArgumentException("Invalid DNS label");
                b.Add((byte)label.Length); b.AddRange(Encoding.ASCII.GetBytes(label));
            }
            b.AddRange(new byte[] { 0, 0, (byte)(ptr ? 12 : 33), 128, 1 });
            return b.ToArray();
        }
        internal static int FindPort(byte[] b, ulong id, out string service)
        {
            service = null;
            if (b.Length < 12 || b.Length > 9000 || (b[2] & 128) == 0 || (b[3] & 15) != 0) return 0;
            try
            {
                int questions = U16(b, 4), records = U16(b, 6) + U16(b, 8) + U16(b, 10), p = 12;
                if (questions > 64 || records > 256) return 0;
                for (int i = 0; i < questions; i++) { Name(b, ref p); if (p + 4 > b.Length) return 0; p += 4; }
                int port = 0;
                for (int i = 0; i < records; i++)
                {
                    string owner = Name(b, ref p);
                    if (p + 10 > b.Length) return 0;
                    int type = U16(b, p), cls = U16(b, p + 2) & 32767, len = U16(b, p + 8);
                    uint ttl = U32(b, p + 4); p += 10;
                    int end = p + len;
                    if (end > b.Length) return 0;
                    if (cls == 1 && ttl > 0 && type == 12 && owner.Equals("_dacp._tcp.local", StringComparison.OrdinalIgnoreCase))
                    {
                        int q = p; string target = Name(b, ref q);
                        if (q > end) return 0;
                        if (Matches(target, id)) service = target;
                    }
                    if (cls == 1 && ttl > 0 && type == 33 && len >= 7 && Matches(owner, id))
                    {
                        int q = p + 6; Name(b, ref q);
                        if (q > end) return 0;
                        port = U16(b, p + 4);
                    }
                    p = end;
                }
                return port;
            }
            catch (InvalidDataException) { return 0; }
        }
        internal static bool? Paused(byte[] b) => Paused(b, 0, b.Length, 0);
        private static bool? Paused(byte[] b, int p, int end, int depth)
        {
            if (depth > 4 || end > 65536) return null;
            bool? found = null;
            while (p + 8 <= end)
            {
                uint n = U32(b, p + 4);
                if (n > end - p - 8) return null;
                string tag = Encoding.ASCII.GetString(b, p, 4); p += 8;
                if (tag == "caps" && n == 1)
                    found = b[p] == 4 ? false : b[p] == 3 || b[p] == 2 ? (bool?)true : null;
                else if (tag == "cmst") found = Paused(b, p, p + (int)n, depth + 1) ?? found;
                p += (int)n;
            }
            return p == end ? found : null;
        }
    }
}
