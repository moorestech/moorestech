using System;
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

            // {p0}を持つ既存キーで位置パラメータ埋めを確認する
            // Verify positional filling with an existing key that carries {p0}
            var text = Localize.GetFormatted(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { "3" });

            StringAssert.Contains("3", text);
            StringAssert.DoesNotContain("{p0}", text);
        }

        [Test]
        public void GetFormattedLeavesTemplateIntactWithoutParams()
        {
            Localize.Initialize();

            Assert.AreEqual(
                Localize.Get(LocalizationKeys.Ui.Common.Close),
                Localize.GetFormatted(LocalizationKeys.Ui.Common.Close, Array.Empty<string>()));
        }
    }
}
