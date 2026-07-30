using System;
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
            ValidateModOrder(modsResource, orderedModIds);

            // マスタと同一順序を明示的に辿り、後のmodを優先する
            // Traverse the explicit master order so later mods take precedence
            foreach (var modId in orderedModIds)
            {
                var rawModId = modId.AsPrimitive();
                var mod = modsResource.Mods[rawModId];

                var csvPath = Path.Combine(mod.ExtractedPath, LocalizationCsvRelativePath);
                if (!File.Exists(csvPath)) continue;
                MergeCsv(File.ReadAllText(csvPath), dictionaries);
            }

            #region Internal

            void ValidateModOrder(
                ModsResource resource,
                IReadOnlyList<ModId> modIds)
            {
                var orderedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var modId in modIds)
                {
                    var rawModId = modId.AsPrimitive();
                    if (!orderedIds.Add(rawModId))
                        throw new InvalidOperationException($"Duplicated mod id in master order: {rawModId}");
                    if (!resource.Mods.ContainsKey(rawModId))
                        throw new InvalidOperationException($"Master order contains an unloaded mod id: {rawModId}");
                }

                // 双方向照合で順序側に欠けたresourceも拒否する
                // Reject resource IDs missing from the order through a reverse membership check
                foreach (var resourceModId in resource.Mods.Keys)
                {
                    if (orderedIds.Contains(resourceModId)) continue;
                    throw new InvalidOperationException($"Loaded mod id is missing from master order: {resourceModId}");
                }
            }

            #endregion
        }

        public static void MergeCsv(
            string csvText,
            Dictionary<string, Dictionary<string, string>> dictionaries)
        {
            var csv = LocalizationCsvParser.Parse(csvText);

            // 予約名・重複・未知言語をmutation前に検証する
            // Validate reserved, duplicate, and unknown languages before mutation
            var seenLanguages = new HashSet<string>(StringComparer.Ordinal);
            foreach (var languageCode in csv.LanguageCodes)
            {
                if (languageCode == Localize.SourcePseudoLocale)
                    throw new LocalizationCsvException($"Reserved localization language: {languageCode}");
                if (!seenLanguages.Add(languageCode))
                    throw new LocalizationCsvException($"Duplicated localization language: {languageCode}");
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
