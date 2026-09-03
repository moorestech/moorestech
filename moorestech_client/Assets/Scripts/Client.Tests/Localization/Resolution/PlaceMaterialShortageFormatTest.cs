using System;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Localization.Resolution
{
    /// <summary>
    /// 設置素材不足行の書式を全言語で固定する
    /// Pins the placement material shortage format across every language
    /// docs/adr/0045 と .decisions/2026-08-30-設置素材不足tooltipはアイテム不足接頭辞を付け所持必要を維持する.md の裁定により、
    /// この行は「接頭辞 + アイテム名 + 所持/必要」の形を保つ。ja「アイテム不足：」/en「Missing item:」といった文言そのものは
    /// 調整の余地を残すため、接頭辞が非空であることだけを検査し literal は固定しない。
    /// Per docs/adr/0045 and the 2026-08-30 ruling, this line keeps the shape "prefix + item name + held/required".
    /// The wording itself (ja / en) stays adjustable, so only a non-empty prefix is asserted, never the literal.
    /// </summary>
    public class PlaceMaterialShortageFormatTest
    {
        private static readonly string ShortageKey = LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage.Key;

        [TestCaseSource(nameof(LanguageCodes))]
        public void ShortageLineKeepsItemNameHeldAndRequiredInOrder(string languageCode)
        {
            var text = GetShortageText(languageCode);

            var itemNameIndex = text.IndexOf("{p0}", StringComparison.Ordinal);
            var heldIndex = text.IndexOf("{p1}", StringComparison.Ordinal);
            var requiredIndex = text.IndexOf("{p2}", StringComparison.Ordinal);

            Assert.Less(-1, itemNameIndex, $"{languageCode}: {ShortageKey} lost the item name placeholder");
            Assert.Less(itemNameIndex, heldIndex, $"{languageCode}: {ShortageKey} must place the held count after the item name");
            Assert.Less(heldIndex, requiredIndex, $"{languageCode}: {ShortageKey} must place the required count after the held count");
        }

        [TestCaseSource(nameof(LanguageCodes))]
        public void ShortageLineKeepsNonEmptyPrefixBeforeItemName(string languageCode)
        {
            var text = GetShortageText(languageCode);

            // 接頭辞が消えるとアイテム名だけの行になり、不足であることが読み取れなくなる
            // Without the prefix the line degrades to a bare item name and no longer reads as a shortage
            var prefix = text.Substring(0, text.IndexOf("{p0}", StringComparison.Ordinal));
            Assert.IsNotEmpty(prefix.Trim(), $"{languageCode}: {ShortageKey} lost its shortage prefix");
        }

        private static string GetShortageText(string languageCode)
        {
            Assert.IsTrue(VanillaLocalizationTable.TryGetLanguage(languageCode, out var table), languageCode);
            Assert.IsTrue(table.TryGetValue(ShortageKey, out var text), $"{languageCode}: {ShortageKey} is missing");
            return text;
        }

        private static string[] LanguageCodes()
        {
            return VanillaLocalizationTable.LanguageCodes;
        }
    }
}
