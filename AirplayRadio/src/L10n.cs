using System;
using System.Globalization;

namespace AirplayRadio
{
    // Set from the game's localization manager on the main thread. English is the fallback.
    public static class L10n
    {
        private static volatile string _locale = "en-US";
        public static string Locale { get => _locale; set => _locale = value ?? "en-US"; }
        public static bool IsChinese(string locale) => string.Equals(locale, "zh-HANT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(locale, "zh-TW", StringComparison.OrdinalIgnoreCase) || string.Equals(locale, "zh-HK", StringComparison.OrdinalIgnoreCase);
        public static string T(string english, string chinese, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, IsChinese(Locale) ? chinese : english, args);
        public static LocalizedText Message(string english, string chinese, params object[] args) => new LocalizedText(english, chinese, args);
        public static LocalizedText ReceiverStatus(string value)
        {
            switch (value)
            {
                case "Stopped": return Message("Stopped", "已停止");
                case "Receiving audio": return Message("Receiving audio", "正在接收音訊");
                case "Waiting for AirPlay — select this receiver on your phone": return Message("Waiting for AirPlay — select this receiver on your phone", "等待 AirPlay 連線，請在手機選擇此接收器");
                case "Connection reset; reconnect from your phone": return Message("Connection reset; reconnect from your phone", "連線已重設，請在手機重新連線");
                case "Unsupported AirPlay audio codec": return Message("Unsupported AirPlay audio codec", "不支援此 AirPlay 音訊格式");
                case "Audio decoder initialization failed": return Message("Audio decoder initialization failed", "音訊解碼器初始化失敗");
                case "Audio conversion failed": return Message("Audio conversion failed", "音訊轉換失敗");
                default: return Message("{0}", "{0}", value ?? "");
            }
        }
    }

    // Keep messages as templates so existing notices also change language without restarting playback.
    public sealed class LocalizedText
    {
        private readonly string _english, _chinese;
        private readonly object[] _args;
        internal LocalizedText(string english, string chinese, object[] args) { _english = english; _chinese = chinese; _args = args; }
        public override string ToString() => L10n.T(_english, _chinese, _args);
        public static implicit operator LocalizedText(string value) => L10n.Message("{0}", "{0}", value ?? "");
    }
}
