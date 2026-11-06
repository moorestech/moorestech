using System;
using System.IO;
using Client.Game.Localization;
using Client.Localization;
using Core.Master;
using Mod.Loader;
using UnityEngine;

namespace Client.Tests.Ui.State.ModeHud
{
    /// <summary>
    /// 一時 mod の辞書へ設置対象の導出キーを合成し、終わったら消して言語を戻す
    /// Composes placement-target derived keys into a temporary mod dictionary, then deletes it and restores the language
    /// </summary>
    public sealed class PlacementNameTestDictionary
    {
        private readonly string _directory;
        private readonly bool _hadSavedLanguage;
        private readonly string _savedLanguage;

        public PlacementNameTestDictionary()
        {
            _hadSavedLanguage = PlayerPrefs.HasKey(Localize.LanguagePreferenceKey);
            _savedLanguage = PlayerPrefs.GetString(Localize.LanguagePreferenceKey);
            _directory = Path.Combine(Path.GetTempPath(), $"mode-hud-names-{Guid.NewGuid():N}");
        }

        public void UseLanguage(string languageCode)
        {
            PlayerPrefs.SetString(Localize.LanguagePreferenceKey, languageCode);
            Localize.Initialize();
        }

        // 行は "key,Source,english,japanese" の CSV 本文（見出し行を除く）
        // Rows are CSV body lines "key,Source,english,japanese" without the header
        public void Merge(string rows)
        {
            var modDirectory = Path.Combine(_directory, "mode-hud-names-mod");
            Directory.CreateDirectory(Path.Combine(modDirectory, "master"));
            Directory.CreateDirectory(Path.Combine(modDirectory, "localization"));
            File.WriteAllText(Path.Combine(modDirectory, "master", "modMeta.json"),
                "{\"id\":\"mode-hud-names\",\"name\":\"mode-hud-names\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            File.WriteAllText(Path.Combine(modDirectory, "localization", "localization.csv"), "key,Source,english,japanese\n" + rows);
            Localize.MergeGameDictionaries(new ModsResource(_directory), new[] { new ModId("author:mode-hud-names") }, MasterSourceTextCollector.Collect());
        }

        public void Restore()
        {
            if (_hadSavedLanguage) PlayerPrefs.SetString(Localize.LanguagePreferenceKey, _savedLanguage);
            else PlayerPrefs.DeleteKey(Localize.LanguagePreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            Localize.Initialize();
        }
    }
}
