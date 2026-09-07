using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Game;
using Game.Audio;
using Game.Audio.Radio;
using Game.SceneFlow;
using Game.UI.InGame;
using LiveRadio.Core;
using UnityEngine;
using UnityEngine.Audio;
using Unity.Entities;
using Game.Settings;

namespace LiveRadio
{
    internal sealed class RadioController : IDisposable
    {
        private const string Network = "LiveRadio";
        private const string Icon = "coui://extendedradio/resources/DefaultIcon.svg";
        private readonly RadioBrowserClient _browser = new RadioBrowserClient();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Dictionary<string, RadioStation> _stations = new Dictionary<string, RadioStation>();
        private readonly GameObject _host;
        private readonly ExtendedRadioAdapter _adapter;
        private readonly Func<string> _ffmpegPath;
        private readonly StationArtwork _artwork;
        private readonly string _artworkDirectory;
        private string _locale;
        private readonly FieldInfo _channels = Require(typeof(Radio), "m_RadioChannels");
        private readonly FieldInfo _descriptors = Require(typeof(Radio), "m_CachedRadioChannelDescriptors");
        private readonly FieldInfo _networks = Require(typeof(Radio), "m_Networks");
        private readonly FieldInfo _player = Require(typeof(Radio), "m_RadioPlayer");
        private readonly FieldInfo _mixer = Require(typeof(Radio.RadioPlayer), "m_RadioGroup");
        private readonly MethodInfo _setClip = typeof(Radio).GetProperty(nameof(Radio.currentClip), ExtendedRadioAdapter.Members)?.GetSetMethod(true) ??
            throw new NotSupportedException("Native radio clip setter not found.");
        public StationLibrary Library { get; }
        public StationCatalog Catalog { get; }
        private Radio _registeredRadio;
        private RadioStation _selected;
        private LiveStreamSession _session;
        private PcmBuffer _playingBuffer;
        private AudioSource _source;
        private AudioClip _clip;
        private bool _disposed, _transportPaused;
        private string _previousNativeName;
        private string _lastDisplay = "";
        private float _nextDisplayUpdate;
        private StreamState? _loggedStreamState;
        private bool _recentRecorded;
        private string _decoderSetting;
        private bool _decoderAvailable;
        private float _nextDecoderCheck;
        private float _nextBufferLog;
#if LIVERADIO_DIAGNOSTICS
        private NativeDecoderProbe _nativeProbe;
        private readonly float[] _diagnosticSamples = new float[1024];
        public void ToggleNativeProbe()
        {
            if (_nativeProbe != null) { _nativeProbe.Dispose(); _nativeProbe = null; return; }
            var radio = _adapter.Radio;
            if (radio == null || !IsLiveChannel(radio.currentChannel?.name))
            { Mod.Log.Info("Native probe: select a Live Radio station first."); return; }
            StopSession();
            ReleaseNativeClip(radio);
            _nativeProbe = new NativeDecoderProbe(_host, (AudioMixerGroup)_mixer.GetValue(_player.GetValue(radio)), Path.GetDirectoryName(_artworkDirectory));
        }
#endif
        public string Status { get; private set; } = L10n.T("Preparing radio.", "正在準備電台。");
        public string SearchStatus => Catalog.Loading ? L10n.T("Searching for stations…", "正在搜尋電台…") : Catalog.Error.Length > 0 ? Catalog.Error :
            L10n.T("Loaded {0} stations. Open the station browser.", "已載入 {0} 台。請開啟找台面板。", Catalog.Cache?.Stations.Count ?? 0);
        private LocalizedText _notice = "";
        public string Notice => _notice.ToString();
        public string SelectedId => _selected?.Id ?? "";
        public string SelectedName => _adapter.Radio?.currentChannel?.name ?? "";
        public bool Paused => _adapter.Radio?.paused ?? true;
        public bool Muted => _adapter.Radio?.muted ?? false;
        public bool Emergency => _adapter.Radio?.hasEmergency ?? false;
        public bool RadioEnabled => SharedSettings.instance.audio.radioActive;
        public float Volume => SharedSettings.instance.audio.radioVolume;
        public bool DecoderAvailable => _decoderAvailable;
        public bool Ready => _adapter.Radio != null;
        public string PlaybackState => _selected == null ? "native" : Paused ? "paused" :
            !RadioEnabled ? "disabled" : _session == null ? "stopped" :
            _session.State == StreamState.Receiving ? (_session.Output?.IsBuffering == false ? "playing" : "buffering") :
            _session.State.ToString().ToLowerInvariant();

        public RadioController(GameObject host, string dataDirectory, ExtendedRadioAdapter adapter, Func<string> ffmpegPath, string name, string country)
        {
            _host = host;
            _adapter = adapter;
            _ffmpegPath = ffmpegPath;
            _artworkDirectory = Path.Combine(dataDirectory, "Icons");
            _artwork = new StationArtwork(_artworkDirectory, _browser);
            Colossal.UI.UIManager.defaultUISystem.AddHostLocation("liveradio-icons", _artworkDirectory, false);
            CatalogQuery initial;
            try { initial = new CatalogQuery { Name = name, Country = country }.Normalize(); }
            catch { initial = new CatalogQuery(); }
            Library = new StationLibrary(Path.Combine(dataDirectory, "stations.json"), initial);
            Catalog = new StationCatalog(Library.State.Query, Library.State.Cache, _browser.BrowseAsync);
        }

        private static FieldInfo Require(Type type, string name) => type.GetField(name, ExtendedRadioAdapter.Members) ??
            throw new NotSupportedException("Native radio integration field not found: " + type.FullName + "." + name);
        private static IEnumerable<RadioStation> Clean(IEnumerable<RadioStation> stations) =>
            RadioStation.DistinctSupported(stations);

        public void Initialize()
        {
            _adapter.Subscribe("OnRadioLoaded", RegisterStations);
            _adapter.Subscribe("OnRadioPaused", OnPaused);
            _adapter.Subscribe("OnRadioUnPaused", OnUnpaused);
            if (_adapter.Radio != null) RegisterStations();
        }

        public void Search(string name, string country)
        {
            Catalog.Search(new CatalogQuery { Name = name, Country = country });
            Library.State.Query = Catalog.Query; Library.Save();
        }

        private void RegisterStations()
        {
            Radio radio = _adapter.Radio;
            if (radio == null || _disposed) return;
            var desired = Clean((_selected == null ? Array.Empty<RadioStation>() : new[] { _selected }).Concat(Library.State.Favorites)).ToArray();
            var channels = (Dictionary<string, Radio.RuntimeRadioChannel>)_channels.GetValue(radio);
            var names = _adapter.CustomNames;
            bool changed = false;
            foreach (string name in _stations.Keys.ToArray())
            {
                var station = _stations[name];
                if (channels.ContainsKey(name) && (name == radio.currentChannel?.name || desired.Any(s => StationLibrary.Same(s, station)))) continue;
                if (channels.TryGetValue(name, out var channel) && channel.network == Network) channels.Remove(name);
                names.RemoveAll(n => n == name); _stations.Remove(name); changed = true;
            }
            changed |= _adapter.AddNetwork(new Radio.RadioNetwork
            {
                name = Network, description = L10n.T("Internet radio from Radio Browser", "Radio Browser 網路直播電台"), icon = Icon, allowAds = false,
            });
            foreach (var station in desired)
            {
                if (_stations.Values.Any(s => StationLibrary.Same(s, station))) continue;
                string baseName = station.Name.Trim() + " · LiveRadio";
                string channelName = baseName;
                int suffix = 2;
                while (channels.ContainsKey(channelName)) channelName = baseName + " (" + suffix++ + ")";
                var channel = new Radio.RadioChannel
                {
                    name = channelName, description = station.CountryCode + " · " + station.Codec +
                        (station.Bitrate > 0 ? " · " + station.Bitrate + " kbps" : ""),
                    icon = _artwork.Get(station), network = Network, uiPriority = _stations.Count,
                    programs = new[] { new Radio.Program { name = L10n.T("Live stream", "網路直播"), description = "Live stream", startTime = "00:00", endTime = "00:00", loopProgram = true, segments = Array.Empty<Radio.Segment>() } },
                };
                if (_adapter.AddChannel(channel)) { _stations.Add(channelName, station); changed = true; }
            }
            _descriptors.SetValue(radio, null);
            _registeredRadio = radio;
            if (changed)
            {
                radio.Reloaded?.Invoke(radio);
                Mod.Log.Info("Native favorites/current station list updated: " + _stations.Count + ".");
            }
        }

        public bool IsLiveChannel(string name) => name != null && _stations.ContainsKey(name);
        private Radio.RuntimeRadioChannel GetNativeChannel(Radio radio) =>
            (_previousNativeName == null ? null : radio.GetRadioChannel(_previousNativeName)) ??
            radio.radioChannelDescriptors.FirstOrDefault(c => c.network != Network);

        public void Tick()
        {
            if (_disposed) return;
#if LIVERADIO_DIAGNOSTICS
            if (_nativeProbe != null)
            {
                if (_nativeProbe.Tick()) { _nativeProbe.Dispose(); _nativeProbe = null; }
                return;
            }
#endif
            bool iconsChanged = _artwork.Tick();
            if (iconsChanged) Library.Save();
            if (iconsChanged || _locale != L10n.Locale)
            {
                _locale = L10n.Locale;
                var nativeRadio = _adapter.Radio;
                if (nativeRadio != null)
                {
                    foreach (var entry in _stations)
                    {
                        var channel = nativeRadio.GetRadioChannel(entry.Key);
                        if (channel == null) continue;
                        channel.icon = _artwork.Get(entry.Value);
                        foreach (var program in channel.schedule ?? Array.Empty<Radio.RuntimeProgram>())
                            program.name = L10n.T("Live stream", "網路直播");
                    }
                    _descriptors.SetValue(nativeRadio, null); nativeRadio.Reloaded?.Invoke(nativeRadio);
                }
            }
            if (Catalog.Tick())
            {
                Library.State.Cache = Catalog.Cache; Library.State.Query = Catalog.Query; Library.Save();
                Mod.Log.Info("Explorer loaded " + Catalog.Cache.Stations.Count + " stations; native selection unchanged.");
            }
            if (_decoderSetting != _ffmpegPath() || Time.unscaledTime >= _nextDecoderCheck)
            {
                _decoderSetting = _ffmpegPath(); _nextDecoderCheck = Time.unscaledTime + 10;
                _decoderAvailable = FfmpegDecoder.FindExecutable(_decoderSetting) != null;
            }

            var radio = _adapter.Radio;
            if (radio == null) return;
            _stations.TryGetValue(radio.currentChannel?.name ?? "", out var selected);
            if (selected?.Id != _selected?.Id)
            {
                StopSession();
                _selected = selected;
                _recentRecorded = false;
                _transportPaused = radio.paused;
                if (selected != null)
                {
                    if (!radio.hasEmergency) ReleaseNativeClip(radio);
                    if (!selected.Custom) _ = _browser.CountSelectionAsync(selected.Id, _lifetime.Token);
                    Mod.Log.Info("Selected live station: " + selected.Name);
                }
                RegisterStations();
            }
            if (selected == null)
            {
                if (radio.currentChannel != null && radio.currentChannel.network != Network) _previousNativeName = radio.currentChannel.name;
                return;
            }
            // An empty live schedule has no next song to replace a finished emergency clip.
            // Clear that completed clip so the original panel exits emergency mode normally.
            var nativePlayer = (Radio.RadioPlayer)_player.GetValue(radio);
            if (radio.hasEmergency && nativePlayer.isCreated && nativePlayer.GetAudioSourceTimeRemaining() <= 0)
            {
                ReleaseNativeClip(radio);
                radio.ClipChanged?.Invoke(radio, null);
            }
            bool allowed = GameManager.instance != null && GameManager.instance.gameMode == GameMode.Game &&
                radio.isEnabled && radio.isActive && !radio.paused && !_transportPaused && !AudioListener.pause;
            if (!allowed) StopSession();
            else
            {
                if (_session == null)
                {
                    Mod.Log.Info("Decoder backend: " + (selected.Format == StreamFormat.Mp3 ? "NLayer (bundled MP3)" : "FFmpeg") + "; format=" + selected.Format + ".");
                    _session = new LiveStreamSession(selected.StreamUri, selected.Format, _ffmpegPath());
                }
                var output = _session.Output;
                if (output != _playingBuffer)
                {
                    if (output != null && _playingBuffer != null &&
                        (output.SampleRate != _playingBuffer.SampleRate || output.Channels != _playingBuffer.Channels))
                        Mod.Log.Info($"Stream audio format changed: {_playingBuffer.SampleRate} Hz/{_playingBuffer.Channels} ch -> {output.SampleRate} Hz/{output.Channels} ch; replacing native output.");
                    DestroyAudio();
                    if (output != null)
                    {
                        _playingBuffer = output;
                        _nextBufferLog = Time.unscaledTime + 5;
                        _source = _host.AddComponent<AudioSource>();
                        _source.playOnAwake = false;
                        _source.spatialBlend = 0;
                        _source.loop = true;
                        _source.outputAudioMixerGroup = (AudioMixerGroup)_mixer.GetValue(_player.GetValue(radio));
                        _clip = AudioClip.Create(selected.Name, output.SampleRate, output.Channels, output.SampleRate, true, data => output.Read(data));
                        _source.clip = _clip;
                        _source.Play();
                        Mod.Log.Info("Started native radio mixer output: " + output.SampleRate + " Hz, " + output.Channels +
                            " channels; mixer=" + (_source.outputAudioMixerGroup?.name ?? "missing") + ".");
                    }
                }
                if (_source != null) _source.mute = radio.muted || radio.hasEmergency;
                if (output != null && Time.unscaledTime >= _nextBufferLog)
                {
                    _nextBufferLog = Time.unscaledTime + 30;
                    LogAudioBuffer(output, "periodic");
#if LIVERADIO_DIAGNOSTICS
                    if (_source != null)
                    {
                        _source.GetOutputData(_diagnosticSamples, 0);
                        double power = 0; foreach (float sample in _diagnosticSamples) power += sample * sample;
                        Mod.Log.Info($"Unity output ({selected.Format}): playing={_source.isPlaying}; rms={Math.Sqrt(power / _diagnosticSamples.Length):F6}.");
                    }
#endif
                }
                if (!_recentRecorded && _session.State == StreamState.Receiving && _session.Output?.IsBuffering == false && !radio.hasEmergency)
                {
                    _recentRecorded = true; Library.RecordSuccess(selected);
                }
                if (_loggedStreamState != _session.State)
                {
                    _loggedStreamState = _session.State;
                    Mod.Log.Info("Stream state: " + _session.State + (_session.State == StreamState.Failed ||
                        _session.State == StreamState.Reconnecting ? "; " + _session.Error : ""));
                }
            }
            if (Time.unscaledTime >= _nextDisplayUpdate)
            {
                _nextDisplayUpdate = Time.unscaledTime + 0.5f;
                var info = GetClipInfo();
                string display = info.title + " | " + info.info;
                if (display != _lastDisplay)
                {
                    _lastDisplay = display;
                    Status = display;
                    radio.ClipChanged?.Invoke(radio, radio.currentClip.m_Asset);
                }
            }
        }

        public RadioUISystem.ClipInfo GetClipInfo()
        {
            var session = _session;
            string state = session == null ? L10n.T("Paused (resume returns to the live broadcast)", "已暫停（恢復時接回直播）") : session.State switch
            {
                StreamState.Connecting => L10n.T("Connecting…", "連線中…"),
                StreamState.Reconnecting => L10n.T("Reconnecting…", "斷線重連中…"),
                StreamState.Failed => L10n.T("Playback failed: {0}", "播放失敗：{0}", session.Error.Length > 240 ? session.Error.Substring(0, 240) : session.Error),
                StreamState.Receiving => session.Output?.IsBuffering == false ? L10n.T("Live", "直播中") : L10n.T("Buffering…", "緩衝中…"),
                _ => L10n.T("Stopped", "已停止"),
            };
            return new RadioUISystem.ClipInfo
            {
                title = _selected?.Name ?? "LiveRadio",
                info = string.IsNullOrEmpty(session?.Title) ? state : state + " · " + session.Title,
            };
        }

        public bool StepStation(Radio radio, int direction)
        {
            if (!IsLiveChannel(radio.currentChannel?.name) || radio.hasEmergency) return false;
            string[] channels = _stations.Keys.ToArray();
            int index = Array.IndexOf(channels, radio.currentChannel.name);
            radio.currentChannel = radio.GetRadioChannel(channels[(index + direction + channels.Length) % channels.Length]);
            return true;
        }

        public void FavoriteCurrent(bool add)
        {
            if (_selected == null) { Status = L10n.T("Select a Live Radio station in the native panel first.", "請先在原生面板選擇一個 LiveRadio 電台。"); return; }
            if (Library.IsFavorite(_selected) != add) Library.Toggle(_selected);
            RegisterStations();
            Status = Library.Notice;
        }

        public RadioStation FindStation(string id) => Library.State.Favorites.Concat(Library.State.Recent)
            .Concat(Catalog.Cache?.Stations ?? Enumerable.Empty<RadioStation>()).Concat(_stations.Values)
            .FirstOrDefault(s => s.Id == id);

        public void ToggleFavorite(string id)
        {
            if (Library.Toggle(FindStation(id))) RegisterStations();
        }
        public void UndoFavorite() { if (Library.Undo()) RegisterStations(); }
        public void AddCustom(string name, string url, string format)
        {
            Library.AddCustom(name, url, format);
            _notice = "";
            RegisterStations();
        }
        public void RecheckDecoder()
        {
            _decoderAvailable = FfmpegDecoder.FindExecutable(_ffmpegPath()) != null;
            _nextDecoderCheck = Time.unscaledTime + 10;
            _notice = _decoderAvailable ? L10n.Message("FFmpeg found. You can now play the station.", "已找到 FFmpeg，現在可以播放電台。") :
                L10n.Message("FFmpeg was not found. Set the full executable path in Options > Live Radio > Audio decoder.", "仍未找到 FFmpeg。請到選項 → Live Radio → 音訊解碼器填入執行檔完整路徑。");
        }
        public void LoadIcons() => _artwork.Open(_stations.Values);
        public string IconFor(RadioStation station) => _artwork.Get(station, createInitial: _stations.Values.Any(s => StationLibrary.Same(s, station)));

        public void Play(string id)
        {
            var station = FindStation(id); var radio = _adapter.Radio;
            if (station == null || !station.IsSupported || radio == null) { _notice = L10n.Message("This station is currently unavailable.", "此電台目前無法播放。"); return; }
            if (radio.hasEmergency) { _notice = L10n.Message("An emergency broadcast is active. You can change stations when it ends.", "緊急廣播中，稍後才能切台。"); return; }
            if (!RadioEnabled) { _notice = L10n.Message("Radio is disabled. Enable the native radio first.", "電台已關閉，請先開啟原生電台。"); return; }
            if (station.Format != StreamFormat.Mp3 && FfmpegDecoder.FindExecutable(_ffmpegPath()) == null)
            { _notice = L10n.Message("This station needs FFmpeg. Set its executable path in Options > Live Radio > Audio decoder.", "此電台需要解碼器，請到 LiveRadio 進階設定指定 FFmpeg。"); return; }
            // Synchronous main-thread registration + native commands: no delayed response can steal a newer selection.
            var previous = _selected;
            _selected = station;
            try { RegisterStations(); } finally { _selected = previous; }
            var entry = _stations.FirstOrDefault(p => StationLibrary.Same(p.Value, station));
            if (entry.Key == null) { _notice = L10n.Message("Could not register the station. Please try again.", "電台註冊失敗，請重試。"); return; }
            var ui = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<RadioUISystem>();
            if (radio.currentChannel?.name != entry.Key)
                typeof(RadioUISystem).GetMethod("SelectStation", ExtendedRadioAdapter.Members).Invoke(ui, new object[] { entry.Key });
            if (radio.paused)
                typeof(RadioUISystem).GetMethod("SetPaused", ExtendedRadioAdapter.Members).Invoke(ui, new object[] { false });
            _transportPaused = false;
            if (StationLibrary.Same(_selected, station) && _session?.State == StreamState.Failed) StopSession();
            _notice = "";
        }

        public void EnableRadio()
        {
            SharedSettings.instance.audio.radioActive = true;
            SharedSettings.instance.audio.ApplyAndSave();
        }

        private void OnPaused() => _transportPaused = true;
        private void OnUnpaused() => _transportPaused = false;
        private void ReleaseNativeClip(Radio radio)
        {
            var oldClip = radio.currentClip;
            ((Radio.RadioPlayer)_player.GetValue(radio)).Play(null);
            _setClip.Invoke(radio, new object[] { default(Radio.ClipInfo) });
            if (oldClip.m_LoadTask != null) oldClip.m_Asset?.Unload();
        }
        private void StopSession()
        {
            if (_playingBuffer != null) LogAudioBuffer(_playingBuffer, "final");
            if (_session != null) { _session.Dispose(); Mod.Log.Info("Stopped live stream and released audio output."); }
            _session = null;
            _loggedStreamState = null;
            DestroyAudio();
        }

        private void LogAudioBuffer(PcmBuffer output, string phase)
        {
            Mod.Log.Info($"Audio buffer ({_selected?.Format}, {phase}): {output.Available / (double)(output.SampleRate * output.Channels):F2}s; underruns={output.Underruns}; dropped={output.DroppedSamples}; lockMisses={output.Contentions}; callbacks={output.ReadCalls}; playedSamples={output.ReadSamples}; silentSamples={output.SilentSamples} (includes initial buffering).");
        }
        private void DestroyAudio()
        {
            if (_source != null) { _source.Stop(); UnityEngine.Object.Destroy(_source); }
            if (_clip != null) UnityEngine.Object.Destroy(_clip);
            _source = null; _clip = null; _playingBuffer = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if LIVERADIO_DIAGNOSTICS
            _nativeProbe?.Dispose(); _nativeProbe = null;
#endif
            Catalog.Dispose();
            _artwork.Dispose();
            Colossal.UI.UIManager.defaultUISystem.RemoveHostLocation("liveradio-icons", _artworkDirectory);
            _lifetime.Cancel();
            StopSession();
            var radio = _registeredRadio;
            if (radio != null)
            {
                if (IsLiveChannel(radio.currentChannel?.name)) radio.currentChannel = GetNativeChannel(radio);
                var channels = (Dictionary<string, Radio.RuntimeRadioChannel>)_channels.GetValue(radio);
                var names = _adapter.CustomNames;
                foreach (string name in _stations.Keys)
                {
                    if (channels.TryGetValue(name, out var channel) && channel.network == Network) channels.Remove(name);
                    names.RemoveAll(n => n == name);
                }
                ((Dictionary<string, Radio.RadioNetwork>)_networks.GetValue(radio)).Remove(Network);
                _descriptors.SetValue(radio, null);
                radio.Reloaded?.Invoke(radio);
            }
            _stations.Clear();
            _lifetime.Dispose();
        }
    }
}
