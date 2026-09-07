using System;
using System.Globalization;

namespace LiveRadio.Core
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
    }

    // Keep messages as templates so existing notices also change language without restarting playback.
    public sealed class LocalizedText
    {
        private readonly string _english, _chinese;
        private readonly object[] _args;
        internal LocalizedText(string english, string chinese, object[] args) { _english = english; _chinese = chinese; _args = args; }
        public override string ToString() => L10n.T(_english, _chinese, _args);
        public static implicit operator LocalizedText(string value) => L10n.Message(value ?? "", value ?? "");
    }
}
