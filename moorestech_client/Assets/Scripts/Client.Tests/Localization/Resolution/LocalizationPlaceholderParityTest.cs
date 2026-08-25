using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizationPlaceholderParityTest
    {
        private static readonly Regex PlaceholderPattern = new(@"\{[^}]*\}");

        [Test]
        public void EveryKeyHasIdenticalPlaceholdersAcrossLanguages()
        {
            // 原文の差し込み枠を正準とし、各言語がそれと同じ集合を持つことを要求する
            // Treat the source placeholders as canonical and require every language to carry the same set
            foreach (var sourceText in VanillaLocalizationTable.SourceTexts)
            {
                var expected = ExtractPlaceholders(sourceText.Value);
                foreach (var languageCode in VanillaLocalizationTable.LanguageCodes)
                {
                    VanillaLocalizationTable.TryGetLanguage(languageCode, out var table);
                    if (!table.TryGetValue(sourceText.Key, out var text)) continue;

                    CollectionAssert.AreEqual(
                        expected,
                        ExtractPlaceholders(text),
                        $"{languageCode}:{sourceText.Key}");
                }
            }
        }

        private static List<string> ExtractPlaceholders(string text)
        {
            return PlaceholderPattern.Matches(text)
                .Select(match => match.Value)
                .OrderBy(placeholder => placeholder, System.StringComparer.Ordinal)
                .ToList();
        }
    }
}
