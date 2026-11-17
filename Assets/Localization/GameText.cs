using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace ProjectMayham.Localization
{
    public static class GameText
    {
        public const string Table = "ProjectMayham";
        public const string Russian = "ru";
        public const string English = "en-US";
        public const string LocalePreference = "Settings.Locale";
        public const string LegacyLanguageIndex = "Settings.LanguageIndex";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!PlayerPrefs.HasKey(LocalePreference) && PlayerPrefs.HasKey(LegacyLanguageIndex))
                PlayerPrefs.SetString(LocalePreference, PlayerPrefs.GetInt(LegacyLanguageIndex) == 1 ? English : Russian);

            _ = LocalizationSettings.InitializationOperation;
        }

        public static string Translate(string source)
        {
            if (string.IsNullOrEmpty(source) || !Application.isPlaying) return source;

            var locale = LocalizationSettings.SelectedLocale;
            if (locale == null || locale.Identifier.Code == Russian) return source;

            var table = LocalizationSettings.StringDatabase.GetTable(Table, locale);
            return table?.GetEntry(source)?.LocalizedValue ?? source;
        }

        public static string Format(string source, params object[] args)
        {
            var locale = Application.isPlaying ? LocalizationSettings.SelectedLocale : null;
            return string.Format(locale?.Identifier.CultureInfo, Translate(source), args);
        }
    }
}
