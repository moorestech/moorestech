using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizeGetFormattedTest
    {
        [Test]
        public void GetFormattedFillsPositionalParams()
        {
            Localize.Initialize();

            // {p0}キーで埋め込みを確認
            // Verify filling an existing {p0} key
            var text = Localize.GetFormatted(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { "3" });

            StringAssert.Contains("3", text);
            StringAssert.DoesNotContain("{p0}", text);
        }

        [Test]
        public void GetFormattedLeavesUnmatchedPlaceholderWhenArgCountIsShort()
        {
            Localize.Initialize();

            // 1個渡すと{p1}が残存する
            // One arg leaves {p1} unmatched
            var text = Localize.GetFormatted(LocalizationKeys.Ui.Loading.TerrainReady, new[] { "3" });

            StringAssert.Contains("3", text);
            StringAssert.Contains("{p1}", text);
        }
    }
}
