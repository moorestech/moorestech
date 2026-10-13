using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mod.Loader;
using Mooresmaster.LocalizationCsv;
using UnityEngine;

namespace Server.Boot.Localization
{
    // 全modのlocalization.csvからitem/fluid名の英語訳だけを集める
    // Collect only English item/fluid names from every mod's localization.csv
    public static class ModEnglishContentNameLoader
    {
        private const string LocalizationCsvRelativePath = "localization/localization.csv";
        private const string EnglishLanguageCode = "english";

        public static Dictionary<string, string> Load(ModsResource modsResource)
        {
            var englishTexts = new Dictionary<string, string>();
            // マスタと同じmod順で後の翻訳を上書きする
            // Overlay later translations in the same mod order as master data
            foreach (var pair in modsResource.Mods.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var csvPath = Path.Combine(pair.Value.ExtractedPath, LocalizationCsvRelativePath);
                if (!File.Exists(csvPath)) continue;
                MergeEnglish(csvPath);
            }
            return englishTexts;

            #region Internal

            void MergeEnglish(string csvPath)
            {
                // modのCSVは外部入力のため、読み込みとパースの失敗をこの境界で隔離する
                // A mod CSV is external input, so read and parse failures are isolated at this boundary
                LocalizationCsv csv;
                try
                {
                    csv = LocalizationCsvParser.Parse(File.ReadAllText(csvPath));
                }
                catch (Exception exception) when (exception is LocalizationCsvException || exception is IOException || exception is UnauthorizedAccessException)
                {
                    Debug.LogError($"[ModEnglishContentName] skipped unreadable localization csv: {csvPath} {exception.Message}");
                    return;
                }

                var englishIndex = Array.IndexOf(csv.LanguageCodes, EnglishLanguageCode);
                if (englishIndex < 0)
                {
                    Debug.Log($"[ModEnglishContentName] english column absent: {csvPath}");
                    return;
                }
                // 特徴名の英語欄だけを辞書へ取り込む
                // Import only the English column of content names
                foreach (var row in csv.Rows)
                {
                    if (!row.Key.StartsWith("item.") && !row.Key.StartsWith("fluid.")) continue;
                    var text = row.Texts[englishIndex];
                    if (string.IsNullOrEmpty(text)) continue;
                    englishTexts[row.Key] = text;
                }
            }

            #endregion
        }

    }
}
