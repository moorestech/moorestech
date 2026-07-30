using System.Collections.Generic;
using System.IO;
using Core.Master;
using Mod.Loader;
using Mooresmaster.LocalizationCsv;

namespace Client.Localization
{
    public static class ModLocalizationMerger
    {
        private const string LocalizationCsvRelativePath = "localization/localization.csv";

        public static void Merge(
            ModsResource modsResource,
            IReadOnlyList<ModId> orderedModIds,
            Dictionary<string, Dictionary<string, string>> dictionaries)
        {
            // マスタと同一順序を明示的に辿り、後のmodを優先する
            // Traverse the explicit master order so later mods take precedence
            foreach (var modId in orderedModIds)
            {
                var rawModId = modId.AsPrimitive();
                if (!modsResource.Mods.TryGetValue(rawModId, out var mod)) continue;

                var csvPath = Path.Combine(mod.ExtractedPath, LocalizationCsvRelativePath);
                if (!File.Exists(csvPath)) continue;
                MergeCsv(File.ReadAllText(csvPath), dictionaries);
            }
        }

        public static void MergeCsv(
            string csvText,
            Dictionary<string, Dictionary<string, string>> dictionaries)
        {
            var csv = LocalizationCsvParser.Parse(csvText);

            // 選択不能な未知言語は入力境界で明示的に拒否する
            // Reject unknown unselectable languages explicitly at the input boundary
            foreach (var languageCode in csv.LanguageCodes)
            {
                if (dictionaries.ContainsKey(languageCode)) continue;
                throw new LocalizationCsvException($"Unsupported localization language: {languageCode}");
            }

            // Sourceと各言語の非空文言だけを既存辞書へ重ねる
            // Overlay only non-empty Source and language texts onto existing dictionaries
            foreach (var row in csv.Rows)
            {
                if (!string.IsNullOrEmpty(row.Source))
                {
                    dictionaries[Localize.SourcePseudoLocale][row.Key] = row.Source;
                }

                for (var languageIndex = 0; languageIndex < csv.LanguageCodes.Length; languageIndex++)
                {
                    var text = row.Texts[languageIndex];
                    if (string.IsNullOrEmpty(text)) continue;
                    dictionaries[csv.LanguageCodes[languageIndex]][row.Key] = text;
                }
            }
        }
    }
}
