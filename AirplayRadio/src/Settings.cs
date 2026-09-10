using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI.Localization;

namespace AirplayRadio
{
    [FileLocation("AirplayRadio")]
    [SettingsUIGroupOrder("Receiver", "Playback", "Info")]
    [SettingsUIShowGroupName("Receiver", "Playback", "Info")]
    public sealed class Settings : ModSetting
    {
        public Settings(IMod mod) : base(mod) { }
        [SettingsUISection("Main", "Receiver")]
        public bool Enabled { get; set; } = true;
        [SettingsUISection("Main", "Playback")]
        public bool RemoteControls { get; set; } = true;
        private int _gainDb;
        [SettingsUISection("Main", "Playback"), SettingsUISlider(min = 0, max = 12, step = 1, unit = "integer")]
        public int GainDb { get => _gainDb; set => _gainDb = System.Math.Max(0, System.Math.Min(12, value)); }
        [SettingsUISection("Main", "Playback")]
        public bool ShowArtwork { get; set; } = true;
        [SettingsUISection("Main", "Receiver"), SettingsUIButton]
        public bool Restart { set => Mod.RestartReceiver?.Invoke(); }
        [SettingsUISection("Main", "Info"), SettingsUIMultilineText]
        [SettingsUIDisplayName(typeof(Settings), nameof(GetStatus))]
        public string Status => Mod.Status.ToString();
        public LocalizedString GetStatus() => LocalizedString.Value(Status);
        [SettingsUISection("Main", "Info"), SettingsUIMultilineText]
        [SettingsUIDisplayName(typeof(Settings), nameof(GetDiagnostics))]
        public string Diagnostics => string.IsNullOrEmpty(Mod.Diagnostics) ? L10n.T("Waiting for audio", "等待音訊") : Mod.Diagnostics;
        public LocalizedString GetDiagnostics() => LocalizedString.Value(Diagnostics);
        public override void SetDefaults() { Enabled = true; RemoteControls = true; GainDb = 0; ShowArtwork = true; }
    }
    internal sealed class Locale : IDictionarySource
    {
        private readonly Settings _settings;
        private readonly bool _chinese;
        internal Locale(Settings settings, bool chinese) { _settings = settings; _chinese = chinese; }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            string Pick(string en, string zh) => _chinese ? zh : en;
            var entries = new Dictionary<string,string> {
                [_settings.GetSettingsLocaleID()] = "Airplay Radio",
                [_settings.GetOptionTabLocaleID("Main")] = "Airplay Radio",
                [_settings.GetOptionGroupLocaleID("Receiver")] = Pick("AirPlay receiver", "AirPlay 接收器"),
                [_settings.GetOptionGroupLocaleID("Playback")] = Pick("Playback", "播放"),
                [_settings.GetOptionGroupLocaleID("Info")] = Pick("Status", "狀態")
            };
            void Add(string key, string en, string zh, string enHelp, string zhHelp)
            {
                entries[_settings.GetOptionLabelLocaleID(key)] = Pick(en, zh);
                entries[_settings.GetOptionDescLocaleID(key)] = Pick(enHelp, zhHelp);
            }
            Add(nameof(Settings.Enabled), "Enable receiver", "啟用接收器", "Select Airplay Radio in the radio panel, then choose it as your phone's AirPlay audio output. Use a trusted local network.", "在電台面板選取 Airplay Radio，再於手機的 AirPlay 音訊輸出清單選擇它。請在可信任的區域網路使用。");
            Add(nameof(Settings.Restart), "Restart receiver", "重新啟動接收器", "Reconnect from your phone after restarting.", "重啟後請在手機重新連線。");
            Add(nameof(Settings.RemoteControls), "Phone media controls", "手機媒體遙控", "Control previous, next and play/pause when the sender supports it. Disable to use local pause only.", "來源支援時，可遙控上一首、下一首及播放／暫停。關閉此選項可改用本機暫停。");
            Add(nameof(Settings.GainDb), "Audio gain (dB)", "音量增益（dB）", "Boost quiet AirPlay audio from 0 to +12 dB. Applies immediately without changing phone volume. Peak limiting may reduce musical dynamics at high gain.", "放大較小聲的 AirPlay 音訊，範圍為 0～+12 dB，立即生效，不改變手機音量。峰值限制器會抑制過大的峰值；高增益可能壓縮音樂動態。");
            Add(nameof(Settings.ShowArtwork), "Show media artwork", "顯示媒體封面", "Display JPEG/PNG artwork sent by the phone; use the radio icon when unavailable. No online image search.", "顯示手機傳來的 JPEG／PNG 封面；未提供時沿用廣播圖示，不會另外上網搜尋圖片。");
            Add(nameof(Settings.Status), "Receiver status", "接收狀態", "Phone control support depends on the sender. Game volume and mute remain local.", "手機遙控功能取決於來源支援；遊戲音量與靜音仍作用於本機。");
            Add(nameof(Settings.Diagnostics), "Audio diagnostics", "音訊診斷", "Counters are logged every 15 seconds during playback. Resends may include repeated requests; input gaps may include phone pauses.", "播放時每 15 秒記錄統計。重送次數可能包含重複請求，輸入間隔也可能包含手機暫停，不能單憑數值認定網路故障。");
            return entries;
        }
        public void Unload() { }
    }
}
