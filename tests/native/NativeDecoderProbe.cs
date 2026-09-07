// Compiled only with /p:LiveRadioDiagnostics=true; never included in release packages.
using System;
using System.IO;
using System.Runtime.Serialization;
using LiveRadio.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace LiveRadio
{
    [DataContract]
    internal sealed class NativeProbeCase
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public bool Live { get; set; }
    }

    internal sealed class NativeDecoderProbe : IDisposable
    {
        private readonly GameObject _host;
        private readonly AudioMixerGroup _mixer;
        private readonly NativeProbeCase[] _cases;
        private readonly float[] _samples = new float[1024];
        private UnityWebRequest _request;
        private DownloadHandlerAudioClip _handler;
        private AudioClip _clip;
        private AudioSource _source;
        private int _index = -1, _nonSilent;
        private float _started, _nextLog, _nextSample, _firstAudio, _nextClipCheck;
        private double _peak;
        private bool _clipErrorLogged;

        public NativeDecoderProbe(GameObject host, AudioMixerGroup mixer, string dataDirectory)
        {
            _host = host; _mixer = mixer;
            using (var input = File.OpenRead(Path.Combine(dataDirectory, "Diagnostics", "native-tests.json")))
                _cases = JsonData.Read<NativeProbeCase[]>(input);
            if (_cases == null || _cases.Length > 12) throw new InvalidDataException("Expected at most 12 native test cases.");
            Mod.Log.Info("NATIVE PROBE BEGIN: Unity only; FFmpeg and NLayer fallback disabled; max 20s startup, 300s live, 16 MiB download per case.");
        }

        public bool Tick()
        {
            try
            {
                if (_request == null)
                {
                    if (++_index >= _cases.Length) { Mod.Log.Info("NATIVE PROBE COMPLETE"); return true; }
                    StartCase();
                }
                float age = Time.realtimeSinceStartup - _started;
                if (_request.downloadedBytes > 16 * 1024 * 1024) return EndCase("download safety limit");
                if (_request.result == UnityWebRequest.Result.ConnectionError ||
                    _request.result == UnityWebRequest.Result.ProtocolError || _request.result == UnityWebRequest.Result.DataProcessingError)
                    return EndCase("request error: " + _request.error);
                if (_clip == null && Time.realtimeSinceStartup >= _nextClipCheck && (_request.downloadedBytes >= 4096 || _request.isDone))
                {
                    _nextClipCheck = Time.realtimeSinceStartup + 1;
                    try { _clip = _handler.audioClip; }
                    catch (Exception ex)
                    {
                        if (!_clipErrorLogged) { _clipErrorLogged = true; Mod.Log.Info("NATIVE PROBE early clip access: " + ex.Message); }
                    }
                    if (_clip != null)
                    {
                        _source = _host.AddComponent<AudioSource>();
                        _source.playOnAwake = false; _source.spatialBlend = 0; _source.loop = false;
                        _source.outputAudioMixerGroup = _mixer; _source.clip = _clip; _source.Play();
                        Mod.Log.Info($"NATIVE PROBE clip created before request completion={!_request.isDone}; length={_clip.length:F2}s; samples={_clip.samples}; rate={_clip.frequency}; channels={_clip.channels}; loadState={_clip.loadState}.");
                    }
                }
                if (_source != null && Time.realtimeSinceStartup >= _nextSample)
                {
                    _nextSample = Time.realtimeSinceStartup + 0.25f;
                    _source.GetOutputData(_samples, 0);
                    double power = 0;
                    foreach (float sample in _samples) power += sample * sample;
                    double rms = Math.Sqrt(power / _samples.Length); _peak = Math.Max(_peak, rms);
                    if (rms > 0.00001) { if (_nonSilent++ == 0) _firstAudio = age; }
                }
                if (Time.realtimeSinceStartup >= _nextLog)
                {
                    _nextLog = Time.realtimeSinceStartup + 10;
                    Mod.Log.Info($"NATIVE PROBE {_cases[_index].Name}: t={age:F1}s; bytes={_request.downloadedBytes}; complete={_request.isDone}; clip={(_clip == null ? "none" : _clip.loadState.ToString())}; playing={_source?.isPlaying}; time={_source?.time:F2}; nonSilent={_nonSilent}; peakRms={_peak:F6}; managedMiB={GC.GetTotalMemory(false) / 1048576d:F1}.");
                }
                if (_nonSilent == 0 && age >= 20) return EndCase("no audio within startup deadline");
                if (_nonSilent > 0 && _source != null && !_source.isPlaying && age > _firstAudio + 1)
                    return EndCase(_cases[_index].Live ? "live clip stopped" : "finite clip completed");
                if (age >= (_cases[_index].Live ? 300 : 25)) return EndCase("observation completed");
            }
            catch (Exception ex) { return EndCase("exception: " + ex.Message); }
            return false;
        }

        private void StartCase()
        {
            var test = _cases[_index];
            var uri = new Uri(test.Url, UriKind.Absolute);
            if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "file") throw new InvalidDataException("Invalid probe URI.");
            AudioType type = test.Type == "MPEG" ? AudioType.MPEG : test.Type == "UNKNOWN" ? AudioType.UNKNOWN : AudioType.OGGVORBIS;
            _started = Time.realtimeSinceStartup; _nextLog = _started + 2; _nextSample = 0; _nextClipCheck = _started + 1;
            _nonSilent = 0; _firstAudio = 0; _peak = 0; _clipErrorLogged = false;
            Mod.Log.Info($"NATIVE PROBE start: {test.Name}; AudioType={type}; live={test.Live}; decoder=Unity; fallback=disabled.");
            _request = UnityWebRequestMultimedia.GetAudioClip(uri.AbsoluteUri, type);
            _request.timeout = 0;
            _handler = (DownloadHandlerAudioClip)_request.downloadHandler;
            _handler.streamAudio = true;
            if (uri.Scheme != "file") { _request.SetRequestHeader("User-Agent", RadioBrowserClient.UserAgent); _request.SetRequestHeader("Icy-MetaData", "0"); }
            _request.SendWebRequest();
        }

        private bool EndCase(string reason)
        {
            Mod.Log.Info($"NATIVE PROBE RESULT {(_index >= 0 && _index < _cases.Length ? _cases[_index].Name : "setup")}: {reason}; nonSilent={_nonSilent}; firstAudio={_firstAudio:F2}s; peakRms={_peak:F6}; bytes={_request?.downloadedBytes}.");
            Release(); return false;
        }

        private void Release()
        {
            if (_source != null) { _source.Stop(); _source.clip = null; UnityEngine.Object.Destroy(_source); }
            if (_request != null) { _request.Abort(); _request.Dispose(); }
            if (_clip != null) UnityEngine.Object.Destroy(_clip);
            _source = null; _request = null; _handler = null; _clip = null;
        }
        public void Dispose() { Release(); Mod.Log.Info("NATIVE PROBE resources released."); }
    }
}
