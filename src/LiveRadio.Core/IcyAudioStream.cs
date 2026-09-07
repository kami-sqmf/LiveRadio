using System;
using System.IO;
using System.Text;

namespace LiveRadio.Core
{
    // Exposes only MP3 bytes to the decoder; ICY metadata can occur between any two frames.
    public sealed class IcyAudioStream : Stream
    {
        private readonly Stream _source;
        private readonly int _interval;
        private readonly Action<string> _onTitle;
        private readonly byte[] _metadata = new byte[255 * 16];
        private int _remaining;
        private long _position;

        public IcyAudioStream(Stream source, int metadataInterval, Action<string> onTitle = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            if (metadataInterval < 0) throw new ArgumentOutOfRangeException(nameof(metadataInterval));
            _interval = _remaining = metadataInterval;
            _onTitle = onTitle;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
            if (count == 0) return 0;
            if (_interval == 0)
            {
                int direct = _source.Read(buffer, offset, count);
                _position += direct;
                return direct;
            }
            if (_remaining == 0)
            {
                int size = _source.ReadByte();
                if (size == -1) return 0;
                int length = size * 16;
                int received = 0;
                while (received < length)
                {
                    int read = _source.Read(_metadata, received, length - received);
                    if (read == 0) throw new EndOfStreamException("Truncated ICY metadata.");
                    received += read;
                }
                string metadata = Encoding.UTF8.GetString(_metadata, 0, length).TrimEnd('\0');
                const string marker = "StreamTitle='";
                int start = metadata.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (start >= 0)
                {
                    start += marker.Length;
                    int end = metadata.IndexOf("';", start, StringComparison.Ordinal);
                    if (end >= start) _onTitle?.Invoke(metadata.Substring(start, Math.Min(end - start, 256)));
                }
                _remaining = _interval;
            }
            int result = _source.Read(buffer, offset, Math.Min(count, _remaining));
            _remaining -= result;
            _position += result;
            return result;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        // NLayer needs a forward byte position even for non-seekable sources.
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
    }
}
