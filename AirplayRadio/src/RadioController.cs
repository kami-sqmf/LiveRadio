using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Game;
using Game.Audio.Radio;
using Game.SceneFlow;
using Game.UI.InGame;
using UnityEngine;
using UnityEngine.Audio;
using Colossal.UI.Binding;
using Unity.Entities;

namespace AirplayRadio
{
    internal sealed class RadioController : IDisposable
    {
        internal const string Channel = "Airplay Radio";
        private const string Network = "AirplayRadio";
        private const string StationIcon = "coui://airplayradio-icons/station.svg";
        private readonly GameObject _host;
        private readonly ExtendedRadioAdapter _adapter;
        private readonly string _dll, _identity, _key;
        private string _iconDirectory;
        private string _artworkDirectory;
        private ReceiverSession _coverSeenReceiver, _coverWorkReceiver, _gainReceiver;
        private ulong _coverSeenRevision, _coverWorkRevision;
        private Task<string> _coverWork;
        private int _appliedGain = -1;
        private readonly Settings _settings;
        private readonly FieldInfo _channels = Field(typeof(Radio), "m_RadioChannels");
        private readonly FieldInfo _networks = Field(typeof(Radio), "m_Networks");
        private readonly FieldInfo _descriptors = Field(typeof(Radio), "m_CachedRadioChannelDescriptors");
        private readonly FieldInfo _player = Field(typeof(Radio), "m_RadioPlayer");
        private readonly FieldInfo _mixer = Field(typeof(Radio.RadioPlayer), "m_RadioGroup");
        private readonly MethodInfo _setClip = typeof(Radio).GetProperty(nameof(Radio.currentClip), ExtendedRadioAdapter.Members)?.GetSetMethod(true);
        private Radio _registered;
        private string _previous;
        private ReceiverSession _receiver;
        private Task<ReceiverSession> _starting;
        private Task _stopping = Task.CompletedTask;
        private AudioSource _source;
        private AudioClip _clip;
        private bool _disposed, _selected, _failed;
        private float _nextDisplay;
        private DacpRemote _remote;
        private string _remoteSnapshot, _lastRemoteStatus;
        private bool _remoteOwnsPause, _savedPause;
        private LocalizedText _controlNote;
        private string _locale;
        private float _noteUntil, _nextDiagnostics, _lastTick, _maxTickGap;
        private readonly FieldInfo _pauseBinding = Field(typeof(RadioUISystem), "m_PausedBinding");
        private string _title = Channel;
        private LocalizedText _info = L10n.Message("Select Airplay Radio to start receiving", "選取 Airplay Radio 以開啟接收");
        internal RadioController(GameObject host, ExtendedRadioAdapter adapter, string dll, string identity, string key, Settings settings)
        { _host = host; _adapter = adapter; _dll = dll; _identity = identity; _key = key; _settings = settings; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, ExtendedRadioAdapter.Members) ?? throw new MissingFieldException(type.FullName, name);
        internal bool Owns(Radio radio) => radio?.currentChannel?.network == Network && radio.currentChannel.name == Channel;
        internal void Initialize()
        {
            _iconDirectory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_dll)), "icons");
            Colossal.UI.UIManager.defaultUISystem.AddHostLocation("airplayradio-icons", _iconDirectory, false);
            try
            {
                string directory = Path.Combine(Path.GetDirectoryName(_key), "Artwork");
                Directory.CreateDirectory(directory);
                Colossal.UI.UIManager.defaultUISystem.AddHostLocation("airplayradio-artwork", directory, false);
                _artworkDirectory = directory;
            }
            catch (Exception ex) { Mod.Log.Warn("Media artwork unavailable: " + ex.Message); }
            _adapter.Subscribe("OnRadioLoaded", Register); _adapter.PatchPanel(); Register();
        }
        private void Register()
        {
            var radio = _adapter.Radio;
            if (radio == null || _disposed) return;
            if (radio.GetRadioChannel(Channel) is Radio.RuntimeRadioChannel existing)
            {
                if (existing.network != Network) throw new InvalidOperationException("Another mod owns the Airplay Radio station name");
                _registered = radio; return;
            }
            _adapter.AddNetwork(new Radio.RadioNetwork { name = Network, description = "Airplay Radio", icon = StationIcon, allowAds = false });
            if (!_adapter.AddChannel(new Radio.RadioChannel {
                name = Channel, network = Network, description = L10n.T("Stream from your iPhone", "從 iPhone 串流音訊"), icon = StationIcon,
                programs = new[] { new Radio.Program { name = L10n.T("AirPlay audio", "AirPlay 音訊"), description = L10n.T("Choose Airplay Radio on your phone", "在手機選擇 Airplay Radio"), startTime = "00:00", endTime = "00:00", loopProgram = true, segments = Array.Empty<Radio.Segment>() } }
            })) throw new InvalidOperationException("Cannot register Airplay Radio station");
            _registered = radio; _descriptors.SetValue(radio, null); radio.Reloaded?.Invoke(radio);
            Mod.Status = L10n.Message("Ready — select Airplay Radio", "已就緒，請選取 Airplay Radio 電台");
        }
        internal void Retry() { _failed = false; Stop(); }
        internal void Tick()
        {
            if (_disposed) return;
            if (_starting?.IsCompleted == true)
            {
                try { _receiver = _starting.GetAwaiter().GetResult(); }
                catch (Exception ex) { _failed = true; _info = L10n.Message("Receiver failed. See Logs/AirplayRadio.log.", "接收器啟動失敗，請查看 Logs/AirplayRadio.log。"); Mod.Log.Warn("Receiver failed: " + ex.Message); }
                _starting = null;
            }
            if (_stopping.IsFaulted) { Mod.Log.Warn("Receiver shutdown failed: " + _stopping.Exception.GetBaseException().Message); _failed = true; _stopping = Task.CompletedTask; }
            var radio = _adapter.Radio;
            if (radio == null) { Stop(); return; }
            if (_registered != radio) Register();
            if (_locale != L10n.Locale)
            {
                _locale = L10n.Locale; _nextDisplay = 0;
                var channel = radio.GetRadioChannel(Channel);
                if (channel != null && channel.network == Network)
                {
                    channel.description = L10n.T("Stream from your iPhone", "從 iPhone 串流音訊");
                    if (_receiver == null)
                        foreach (var program in channel.schedule ?? Array.Empty<Radio.RuntimeProgram>())
                        {
                            program.name = L10n.T("AirPlay audio", "AirPlay 音訊");
                            program.description = L10n.T("Choose Airplay Radio on your phone", "在手機選擇 Airplay Radio");
                        }
                    _descriptors.SetValue(radio, null); radio.Reloaded?.Invoke(radio);
                }
            }
            bool selected = Owns(radio);
            if (!selected) _previous = radio.currentChannel?.name;
            if (selected != _selected)
            {
                if (selected) _savedPause = radio.paused;
                else if (_remoteOwnsPause) { SetPausedState(radio, _savedPause); _remoteOwnsPause = false; }
                _selected = selected; _failed = false;
                if (selected && !radio.hasEmergency) ReleaseClip(radio);
            }
            bool desired = selected && _settings.Enabled && GameManager.instance.gameMode == GameMode.Game && radio.isEnabled && radio.isActive;
            if (!desired) { Stop(); return; }
            if (_lastTick > 0) _maxTickGap = Math.Max(_maxTickGap, Time.unscaledTime - _lastTick);
            _lastTick = Time.unscaledTime;
            if (_receiver == null && _starting == null && _stopping.IsCompleted && !_failed)
            {
                string name = "Airplay Radio - " + Environment.MachineName;
                if (name.Length > 40) name = name.Substring(0, 40);
                _info = L10n.Message("Starting AirPlay receiver", "正在啟動 AirPlay 接收器");
                _starting = Task.Run(() => ReceiverSession.Start(_dll, name, _identity, _key));
            }
            if (_receiver != null && _source == null)
            {
                var receiver = _receiver;
                ApplyGain();
                _source = _host.AddComponent<AudioSource>(); _source.playOnAwake = false;
                _source.spatialBlend = 0; _source.loop = true;
                _source.outputAudioMixerGroup = (AudioMixerGroup)_mixer.GetValue(_player.GetValue(radio));
                _clip = AudioClip.Create(Channel, 44100, 2, 44100, true, receiver.Read);
                _source.clip = _clip; _source.Play();
            }
            if (_source != null)
            {
                ApplyGain();
                // Remote pause stops the sender. Do not keep muting locally if a basic
                // sender resumes on the phone without offering playback-state polling.
                bool muted = radio.muted || (radio.paused && !_remoteOwnsPause) || radio.hasEmergency || AudioListener.pause;
                _source.mute = muted;
                if (muted) _receiver.Clear();
            }
            if (radio.hasEmergency && ((Radio.RadioPlayer)_player.GetValue(radio)).isCreated &&
                ((Radio.RadioPlayer)_player.GetValue(radio)).GetAudioSourceTimeRemaining() <= 0)
            { ReleaseClip(radio); radio.ClipChanged?.Invoke(radio, null); }
            if (Time.unscaledTime >= _nextDisplay)
            {
                _nextDisplay = Time.unscaledTime + 0.5f;
                RefreshArtwork(radio);
                if (_receiver != null)
                {
                    RefreshRemote(radio);
                    string title = _receiver.GetText(1), artist = _receiver.GetText(2), client = _receiver.GetText(3);
                    _title = string.IsNullOrWhiteSpace(title) ? Channel : title;
                    _info = radio.paused ? (_remoteOwnsPause ? L10n.T("Phone paused", "手機已暫停") : L10n.T("Locally paused — phone keeps playing", "本機已暫停，手機繼續播放")) : L10n.ReceiverStatus(_receiver.GetText(0));
                    if (artist.Length > 0) _info += " · " + artist;
                    if (client.Length > 0) _info += " · " + client;
                    if (Time.unscaledTime < _noteUntil) _info += " · " + _controlNote;
                }
                Mod.Status = L10n.Message("{0}\n{1}", "{0}\n{1}", _info, _remote?.Status ?? L10n.Message("Phone control unavailable; local pause", "無手機遙控，使用本機暫停"));
                UpdateProgram(radio);
                if (!radio.hasEmergency) radio.ClipChanged?.Invoke(radio, radio.currentClip.m_Asset);
            }
            if (_receiver != null && Time.unscaledTime >= _nextDiagnostics)
            {
                _nextDiagnostics = Time.unscaledTime + 15;
                Mod.Diagnostics = $"bufferMs={_receiver.Stat(0) * 1000 / 88200}; underruns={_receiver.Stat(1)}; ringContention={_receiver.Stat(2)}; decodeErrors={_receiver.Stat(3)}; resendPackets={_receiver.Stat(4)}; maxInputGapMs={_receiver.Stat(5)}; droppedSamples={_receiver.Stat(6)}; lifecycleReadMisses={_receiver.ReadMisses}; maxMainThreadGapMs={(int)(_maxTickGap * 1000)}; gainDb={_appliedGain}; limitedFrames={_receiver.Gain.LimitedFrames}";
                if (_source != null && !_source.mute && !radio.paused && _receiver.Decoded > 0) Mod.Log.Info("Audio health: " + Mod.Diagnostics);
                _maxTickGap = 0;
            }
        }
        private void ApplyGain()
        {
            if (_receiver == null || (_gainReceiver == _receiver && _appliedGain == _settings.GainDb)) return;
            _receiver.Gain.SetDecibels(_settings.GainDb);
            _gainReceiver = _receiver; _appliedGain = _settings.GainDb;
            Mod.Log.Info("Local audio gain: +" + _appliedGain + " dB");
        }
        private void RefreshArtwork(Radio radio)
        {
            if (_coverWork?.IsCompleted == true)
            {
                string path = null;
                try { path = _coverWork.GetAwaiter().GetResult(); }
                catch (Exception ex) { Mod.Log.Warn("Media artwork skipped: " + ex.Message); }
                _coverWork = null;
                // A completed thumbnail must never replace artwork from a newer connection or song.
                if (_receiver != null && _receiver == _coverWorkReceiver && _settings.ShowArtwork &&
                    _receiver.CoverRevision == _coverWorkRevision)
                {
                    SetArtwork(radio, path);
                    if (path != null) Mod.Log.Info("Media artwork updated (phone JPEG/PNG, circular thumbnail)");
                }
                _coverWorkReceiver = null;
            }
            if (!_settings.ShowArtwork || _receiver == null || _artworkDirectory == null)
            {
                _coverSeenReceiver = null;
                SetArtwork(radio, null);
                return;
            }
            if (_coverWork != null) return;
            ulong revision = _receiver.CoverRevision;
            if (_coverSeenReceiver == _receiver && _coverSeenRevision == revision) return;
            _coverSeenReceiver = _receiver; _coverSeenRevision = revision;
            if (revision == 0) { SetArtwork(radio, null); return; }
            var receiver = _receiver;
            string directory = _artworkDirectory;
            _coverWorkReceiver = receiver; _coverWorkRevision = revision;
            _coverWork = Task.Run(() => MediaArtwork.Save(receiver.GetCover(revision), directory));
        }
        private void SetArtwork(Radio radio, string path)
        {
            var channel = radio?.GetRadioChannel(Channel);
            if (channel == null || channel.network != Network) return;
            string icon = path == null ? StationIcon : "coui://airplayradio-artwork/" + Path.GetFileName(path);
            if (channel.icon == icon) return;
            channel.icon = icon;
            _descriptors.SetValue(radio, null);
            radio.Reloaded?.Invoke(radio);
        }
        private void Note(LocalizedText text) { _controlNote = text; _noteUntil = Time.unscaledTime + 6; Mod.Status = text; _nextDisplay = 0; }
        internal bool Step(Radio radio, bool next)
        {
            if (!Owns(radio) || radio.hasEmergency) return false;
            RefreshRemote(radio);
            if (_remote?.Send(next ? RemoteCommand.Next : RemoteCommand.Previous) == true)
                Note(L10n.Message("Sending to phone", "正在通知手機"));
            else Note(_remote?.Ready == true ? L10n.Message("Phone control busy", "手機遙控處理中") : L10n.Message("Change tracks on phone", "手機未提供遙控，請在手機切歌"));
            return true;
        }
        internal bool PauseFromUI(bool paused)
        {
            var radio = _adapter.Radio;
            if (!Owns(radio) || radio.hasEmergency) return false;
            RefreshRemote(radio);
            if (_remote?.Ready != true)
            {
                _remoteOwnsPause = false;
                Note(L10n.Message("Local pause only", "僅在本機暫停，不控制手機"));
                return false;
            }
            Note(_remote.Send(paused ? RemoteCommand.Pause : RemoteCommand.Play)
                ? L10n.Message("Sending to phone", "正在通知手機") : L10n.Message("Phone control busy", "手機遙控處理中"));
            return true;
        }
        private void RefreshRemote(Radio radio)
        {
            string snapshot = _settings.RemoteControls ? _receiver?.GetText(4) : "";
            if (snapshot != _remoteSnapshot)
            {
                _remote?.Dispose(); _remote = null; _remoteSnapshot = snapshot;
                var identity = snapshot == null ? null : RemoteIdentity.Parse(snapshot);
                if (identity != null) _remote = new DacpRemote(identity);
                if (_remoteOwnsPause) { SetPausedState(radio, _savedPause); _remoteOwnsPause = false; }
            }
            if (_remote == null) return;
            while (_remote.TryResult(out var result))
            {
                Note(result.Success ? L10n.Message("Phone accepted control", "手機已接受控制") : _remote.Status);
                Mod.Log.Info("Remote command " + result.Command + ": HTTP " + result.Code);
            }
            if (_remote.Ready && _remote.Paused.HasValue)
            {
                if (_remote.Paused.Value && !radio.paused) _receiver?.Clear();
                SetPausedState(radio, _remote.Paused.Value); _remoteOwnsPause = true;
            }
            if (_lastRemoteStatus != _remote.Status.ToString())
            {
                _lastRemoteStatus = _remote.Status.ToString(); Mod.Log.Info("Remote: " + _lastRemoteStatus);
            }
        }
        private void SetPausedState(Radio radio, bool paused)
        {
            if (radio == null) return;
            radio.paused = paused;
            var ui = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<RadioUISystem>();
            if (ui != null) ((ValueBinding<bool>)_pauseBinding.GetValue(ui)).Update(paused);
        }
        internal RadioUISystem.ClipInfo ClipInfo() => new RadioUISystem.ClipInfo { title = _title, info = _info.ToString() };
        private void UpdateProgram(Radio radio)
        {
            if (!Owns(radio) || radio.hasEmergency) return;
            bool changed = false;
            foreach (var program in radio.currentChannel.schedule ?? Array.Empty<Radio.RuntimeProgram>())
            {
                if (program.name == _title && program.description == _info.ToString()) continue;
                program.name = _title;
                program.description = _info.ToString();
                changed = true;
            }
            if (changed) radio.ProgramChanged?.Invoke(radio);
        }
        private void ReleaseClip(Radio radio)
        {
            if (_setClip == null) throw new MissingMethodException("Native radio clip setter");
            var old = radio.currentClip;
            ((Radio.RadioPlayer)_player.GetValue(radio)).Play(null);
            _setClip.Invoke(radio, new object[] { default(Radio.ClipInfo) });
            if (old.m_LoadTask != null) old.m_Asset?.Unload();
        }
        private void Stop()
        {
            _coverSeenReceiver = null; _gainReceiver = null;
            SetArtwork(_registered, null);
            _remote?.Dispose(); _remote = null; _remoteSnapshot = null; _lastRemoteStatus = null;
            if (_remoteOwnsPause) { SetPausedState(_adapter.Radio, _savedPause); _remoteOwnsPause = false; }
            _lastTick = 0; _maxTickGap = 0;
            if (_source != null) { _source.Stop(); UnityEngine.Object.Destroy(_source); _source = null; }
            if (_clip != null) { UnityEngine.Object.Destroy(_clip); _clip = null; }
            if (_receiver != null) { var old = _receiver; _receiver = null; _stopping = Task.Run(() => old.Dispose()); }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; Stop();
            // A pending start owns its own cleanup even after the mod unloads.
            if (_starting != null) _starting.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Dispose(); else _ = t.Exception; });
            if (_coverWork != null) _coverWork.ContinueWith(t => { if (t.IsFaulted) _ = t.Exception; });
            _adapter.Dispose();
            if (_iconDirectory != null) Colossal.UI.UIManager.defaultUISystem.RemoveHostLocation("airplayradio-icons", _iconDirectory);
            if (_artworkDirectory != null) Colossal.UI.UIManager.defaultUISystem.RemoveHostLocation("airplayradio-artwork", _artworkDirectory);
            var radio = _registered;
            if (radio != null)
            {
                if (Owns(radio)) radio.currentChannel = _previous == null ? null : radio.GetRadioChannel(_previous);
                var channels = (Dictionary<string, Radio.RuntimeRadioChannel>)_channels.GetValue(radio);
                if (channels.TryGetValue(Channel, out var channel) && channel.network == Network) channels.Remove(Channel);
                _adapter.CustomNames.RemoveAll(n => n == Channel);
                ((Dictionary<string, Radio.RadioNetwork>)_networks.GetValue(radio)).Remove(Network);
                _descriptors.SetValue(radio, null); radio.Reloaded?.Invoke(radio);
            }
        }
    }
    internal static class CurrentBroadcastPatch
    {
        private static bool Prefix(Radio radio, ref RadioUISystem.ClipInfo __result)
        {
            var c = Mod.Controller;
            if (c == null || !c.Owns(radio) || radio.hasEmergency) return true;
            __result = c.ClipInfo(); return false;
        }
    }
    internal static class RemoteNextPatch
    {
        private static bool Prefix(Radio __instance) => !(Mod.Controller?.Step(__instance, true) ?? false);
    }
    internal static class RemotePreviousPatch
    {
        private static bool Prefix(Radio __instance) => !(Mod.Controller?.Step(__instance, false) ?? false);
    }
    internal static class RemotePausePatch
    {
        private static bool Prefix(bool paused) => !(Mod.Controller?.PauseFromUI(paused) ?? false);
    }
}
