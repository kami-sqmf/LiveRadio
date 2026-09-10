using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AirplayRadio
{
    // All native work stays inside this process. The module remains loaded for
    // the process lifetime; unloading code used by Unity audio callbacks is unsafe.
    internal sealed class NativeApi
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr CreateFn();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int StartFn(IntPtr h, byte[] name, byte[] id, byte[] path);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void HandleFn(IntPtr h);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ReadFn(IntPtr h, IntPtr output, int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int TextFn(IntPtr h, int field, byte[] output, int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate ulong CountFn(IntPtr h);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate ulong StatFn(IntPtr h, int field);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CoverFn(IntPtr h, ulong revision, [Out] byte[] output, int length);
        internal readonly CreateFn Create;
        internal readonly StartFn Start;
        internal readonly HandleFn Destroy, Clear;
        internal readonly ReadFn Read;
        internal readonly TextFn Text;
        internal readonly CountFn Decoded;
        internal readonly StatFn Stat;
        internal readonly CountFn CoverRevision;
        internal readonly CoverFn Cover;
        internal NativeApi(string path)
        {
            // Search only the DLL's own directory and safe default locations.
            IntPtr module = LoadLibraryEx(Path.GetFullPath(path), IntPtr.Zero, 0x100 | 0x1000);
            if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot load Airplay Radio native receiver or its dependencies");
            T Bind<T>(string name) where T : class
            {
                var p = GetProcAddress(module, name);
                if (p == IntPtr.Zero) throw new EntryPointNotFoundException(name);
                return (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T));
            }
            Create = Bind<CreateFn>("ar_create"); Start = Bind<StartFn>("ar_start");
            Destroy = Bind<HandleFn>("ar_destroy"); Clear = Bind<HandleFn>("ar_clear");
            Read = Bind<ReadFn>("ar_read"); Text = Bind<TextFn>("ar_text"); Decoded = Bind<CountFn>("ar_decoded_samples");
            Stat = Bind<StatFn>("ar_stat");
            CoverRevision = Bind<CountFn>("ar_cover_revision"); Cover = Bind<CoverFn>("ar_cover");
        }
    }
    internal sealed class ReceiverSession : IDisposable
    {
        private static readonly object ApiGate = new object();
        private static NativeApi _api;
        // Metadata and audio are concurrent readers. Only disposal excludes readers.
        private readonly ReaderWriterLockSlim _lifetime = new ReaderWriterLockSlim();
        private readonly object _textGate = new object();
        private long _readMisses;
        internal long ReadMisses => Interlocked.Read(ref _readMisses);
        internal readonly LocalGain Gain = new LocalGain();
        private IntPtr _handle;
        private readonly byte[] _text = new byte[1024];
        private ReceiverSession(IntPtr handle) { _handle = handle; }
        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s + "\0");
        internal static ReceiverSession Start(string dll, string name, string id, string keyPath)
        {
            lock (ApiGate) { if (_api == null) _api = new NativeApi(dll); }
            var h = _api.Create();
            if (h == IntPtr.Zero) throw new InvalidOperationException("Cannot allocate receiver");
            var receiver = new ReceiverSession(h);
            try
            {
                int code = _api.Start(h, Utf8(name), Utf8(id), Utf8(keyPath));
                if (code != 0) throw new InvalidOperationException("Receiver startup failed (" + code + "): " + receiver.GetText(0));
                return receiver;
            }
            catch { receiver.Dispose(); throw; }
        }
        internal string GetText(int field)
        {
            lock (_textGate)
            {
                _lifetime.EnterReadLock();
                try {
                    if (_handle == IntPtr.Zero) return "";
                    int n = _api.Text(_handle, field, _text, _text.Length);
                    return Encoding.UTF8.GetString(_text, 0, Math.Max(0, Math.Min(n, _text.Length)));
                } finally { _lifetime.ExitReadLock(); }
            }
        }
        internal ulong Decoded { get { _lifetime.EnterReadLock(); try { return _handle == IntPtr.Zero ? 0 : _api.Decoded(_handle); } finally { _lifetime.ExitReadLock(); } } }
        internal ulong CoverRevision { get { _lifetime.EnterReadLock(); try { return _handle == IntPtr.Zero ? 0 : _api.CoverRevision(_handle); } finally { _lifetime.ExitReadLock(); } } }
        internal byte[] GetCover(ulong revision)
        {
            _lifetime.EnterReadLock();
            try {
                if (_handle == IntPtr.Zero) return null;
                int size = _api.Cover(_handle, revision, null, 0);
                if (size < 0 || size > 2 * 1024 * 1024) return null;
                if (size == 0) return Array.Empty<byte>();
                var bytes = new byte[size];
                return _api.Cover(_handle, revision, bytes, bytes.Length) == size ? bytes : null;
            } finally { _lifetime.ExitReadLock(); }
        }
        internal ulong Stat(int field) {
            _lifetime.EnterReadLock();
            try { return _handle == IntPtr.Zero ? 0 : _api.Stat(_handle, field); }
            finally { _lifetime.ExitReadLock(); }
        }
        internal unsafe void Read(float[] samples)
        {
            if (!_lifetime.TryEnterReadLock(0)) { Interlocked.Increment(ref _readMisses); Array.Clear(samples, 0, samples.Length); return; }
            try
            {
                if (_handle == IntPtr.Zero) { Array.Clear(samples, 0, samples.Length); return; }
                fixed (float* p = samples) _api.Read(_handle, (IntPtr)p, samples.Length);
                Gain.Process(samples);
            }
            finally { _lifetime.ExitReadLock(); }
        }
        internal void Clear() {
            _lifetime.EnterReadLock();
            try { if (_handle != IntPtr.Zero) _api.Clear(_handle); }
            finally { _lifetime.ExitReadLock(); }
        }
        public void Dispose()
        {
            IntPtr old;
            _lifetime.EnterWriteLock();
            try { old = _handle; _handle = IntPtr.Zero; }
            finally { _lifetime.ExitWriteLock(); }
            // Readers can no longer access this handle. Join network workers
            // without holding the managed gate or blocking the audio callback.
            if (old != IntPtr.Zero) _api.Destroy(old);
        }
    }
}
