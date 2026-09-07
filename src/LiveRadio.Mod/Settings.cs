using System.Collections.Generic;
using LiveRadio.Core;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI.Localization;

namespace LiveRadio
{
    [FileLocation("LiveRadio")]
    [SettingsUIGroupOrder("Search", "Favorites", "Decoder", "Info")]
    [SettingsUIShowGroupName("Search", "Favorites", "Decoder", "Info")]
    public sealed class Settings : ModSetting
    {
        public Settings(IMod mod) : base(mod) { }

        [SettingsUISection("Main", "Search")]
        [SettingsUIButton]
        public bool OpenBrowser { set => RadioBrowserUISystem.OpenBrowser(); }

        [SettingsUISection("Main", "Search")]
        [SettingsUIHidden]
        public string SearchName { get; set; } = "";

        [SettingsUISection("Main", "Search")]
        [SettingsUIHidden]
        public string CountryCode { get; set; } = "TW";

        [SettingsUISection("Main", "Search")]
        [SettingsUIHidden]
        public bool Search { set { ApplyAndSave(); Mod.Controller?.Search(SearchName, CountryCode); } }

        [SettingsUISection("Main", "Search")]
        [SettingsUIMultilineText]
        [SettingsUIDisplayName(typeof(Settings), nameof(GetSearchStatusText))]
        public string SearchStatus => Mod.Controller?.SearchStatus ?? Mod.BootstrapStatus;

        public LocalizedString GetSearchStatusText() => LocalizedString.Value(SearchStatus);

        [SettingsUISection("Main", "Favorites")]
        [SettingsUIHidden]
        public bool AddFavorite { set => Mod.Controller?.FavoriteCurrent(true); }

        [SettingsUISection("Main", "Favorites")]
        [SettingsUIHidden]
        public bool RemoveFavorite { set => Mod.Controller?.FavoriteCurrent(false); }

        [SettingsUISection("Main", "Decoder")]
        [SettingsUITextInput]
        public string FfmpegPath { get; set; } = "";

        internal string EffectiveFfmpegPath =>
#if LIVERADIO_DIAGNOSTICS
            DisableFfmpegForTest ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LiveRadio-missing-decoder-test.exe") :
#endif
            FfmpegPath;

#if LIVERADIO_DIAGNOSTICS
        [SettingsUISection("Main", "Decoder")]
        public bool DisableFfmpegForTest { get; set; }

        [SettingsUISection("Main", "Decoder")]
        [SettingsUIButton]
        public bool NativeProbe { set => Mod.Controller?.ToggleNativeProbe(); }
#endif

        [SettingsUISection("Main", "Decoder")]
        [SettingsUIMultilineText]
        [SettingsUIDisplayName(typeof(Settings), nameof(GetDecoderStatusText))]
        public string DecoderStatus => LiveRadio.Core.FfmpegDecoder.FindExecutable(EffectiveFfmpegPath) is string path
            ? L10n.T("FFmpeg found: {0}", "已找到 FFmpeg：{0}", path) : L10n.T("FFmpeg not found. MP3 works without it; AAC, OGG, HLS and Auto need FFmpeg.", "未找到 FFmpeg。MP3 可直接播放；AAC、OGG、HLS 與自動偵測需要 FFmpeg。");

        public LocalizedString GetDecoderStatusText() => LocalizedString.Value(DecoderStatus.Replace('\\', '/'));

        [SettingsUISection("Main", "Info")]
        [SettingsUIMultilineText]
        [SettingsUIDisplayName(typeof(Settings), nameof(GetStatusText))]
        public string Status => Mod.Controller?.Status ?? Mod.BootstrapStatus;

        public LocalizedString GetStatusText() => LocalizedString.Value(Status);

        public override void SetDefaults() { SearchName = ""; CountryCode = "TW"; FfmpegPath = ""; }
    }

    public sealed class Locale : IDictionarySource
    {
        private readonly Settings _settings;
        private readonly bool _chinese;
        public Locale(Settings settings, bool chinese) { _settings = settings; _chinese = chinese; }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            string Pick(string en, string zh) => _chinese ? zh : en;
            var entries = new Dictionary<string, string>
            {
                [_settings.GetSettingsLocaleID()] = "Live Radio",
                [_settings.GetOptionTabLocaleID("Main")] = Pick("Live radio", "網路電台"),
                [_settings.GetOptionGroupLocaleID("Search")] = Pick("Station browser", "電台瀏覽"),
                [_settings.GetOptionGroupLocaleID("Favorites")] = Pick("Favorites", "收藏"),
                [_settings.GetOptionGroupLocaleID("Decoder")] = Pick("Audio decoder", "音訊解碼器"),
                [_settings.GetOptionGroupLocaleID("Info")] = Pick("Status", "狀態"),
            };
            void Add(string key, string en, string zh, string enHelp, string zhHelp)
            {
                entries[_settings.GetOptionLabelLocaleID(key)] = Pick(en, zh);
                entries[_settings.GetOptionDescLocaleID(key)] = Pick(enHelp, zhHelp);
            }
            Add(nameof(Settings.OpenBrowser), "Open station browser", "開啟找台面板", "Return to the city to use Favorites, Explore and Recent in the native radio panel.", "返回城市後，在原生電台面板使用收藏、探索與最近收聽。預設顯示本機收藏，進入探索才載入清單。");
            Add(nameof(Settings.SearchName), "Station name", "電台名稱", "Leave empty to list popular stations.", "留空可列出熱門電台。");
            Add(nameof(Settings.CountryCode), "Country code", "國家代碼", "Two letters (TW, JP, US); empty searches worldwide.", "輸入 TW、JP、US 等兩碼代碼；留空搜尋全球。");
            Add(nameof(Settings.Search), "Search stations", "搜尋電台", "Add up to 100 unique MP3, AAC and OGG stations to the native LiveRadio network. Favorites stay available.", "搜尋最多 100 個不重複的 MP3、AAC、OGG 電台，加入原生面板的 LiveRadio 網路；保留收藏電台。");
            Add(nameof(Settings.SearchStatus), "Search results", "搜尋結果", "Unsupported entries are counted within the pages checked. Refine the name when results reach the limit.", "顯示已查詢頁面內不支援的項目數量。結果達上限時，請輸入更精確的名稱。");
            Add(nameof(Settings.AddFavorite), "Favorite current station", "收藏目前電台", "Keep the selected LiveRadio station between searches.", "保存原生面板目前選取的 LiveRadio 電台，之後搜尋仍會保留。");
            Add(nameof(Settings.RemoveFavorite), "Unfavorite current station", "取消收藏目前電台", "Remove the selected station from favorites.", "從收藏中移除目前選取的電台。");
            Add(nameof(Settings.FfmpegPath), "FFmpeg executable", "FFmpeg 執行檔路徑", "Leave empty for automatic discovery, or enter the full path to ffmpeg.exe (not a launcher). Pause and resume the station after changing this.", "留空會自動尋找；也可填入 ffmpeg.exe 本體的完整路徑。變更後請暫停再恢復電台。");
            Add(nameof(Settings.DecoderStatus), "Decoder status", "解碼器狀態", "AAC, OGG (Vorbis/Opus), HLS and Auto use your separately installed FFmpeg. MP3 uses NLayer included with this mod, not the game's native decoder.", "AAC、OGG（Vorbis／Opus）、HLS 與自動偵測使用另外安裝的 FFmpeg；MP3 使用本模組內附的 NLayer，並非遊戲原生解碼器。");
            Add(nameof(Settings.Status), "Status", "狀態", "Use the native radio panel to select a station, pause, mute, and change volume.", "在原生電台面板選台、暫停、靜音與調整音量。");
#if LIVERADIO_DIAGNOSTICS
            Add(nameof(Settings.DisableFfmpegForTest), "Test without FFmpeg", "測試：停用 FFmpeg", "Diagnostic build only. Pause and resume after changing. Does not change the installed executable.", "僅診斷版。變更後暫停再恢復播放，不會變更已安裝的執行檔。");
            Add(nameof(Settings.NativeProbe), "Start / stop native decoder test", "開始／停止原生解碼測試", "Select a Live Radio station first. Runs isolated Unity tests without FFmpeg or NLayer; results are written to LiveRadio.log.", "請先選擇 Live Radio 電台。獨立測試 Unity 解碼，不使用 FFmpeg 或 NLayer，結果記錄於 LiveRadio.log。");
#endif
            return entries;
        }
        public void Unload() { }
    }
}
