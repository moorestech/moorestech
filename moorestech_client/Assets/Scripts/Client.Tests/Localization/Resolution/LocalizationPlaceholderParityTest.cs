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
            // 原文の枠集合を各言語へ要求
            // Requires the source placeholder set in every language
            foreach (var sourceText in VanillaLocalizationTable.SourceTexts)
            {
                var expected = ExtractPlaceholders(sourceText.Value);
                foreach (var languageCode in VanillaLocalizationTable.LanguageCodes)
                {
                    Assert.IsTrue(VanillaLocalizationTable.TryGetLanguage(languageCode, out var table), languageCode);
                    Assert.IsTrue(table.TryGetValue(sourceText.Key, out var text), $"{languageCode}:{sourceText.Key}");

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
