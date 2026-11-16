using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Localization.Resolution
{
    public class LocalizeGetFormattedTest
    {
        [Test]
        public void GetFormattedFillsNamedParams()
        {
            Localize.Initialize();

            var text = Localize.GetFormatted(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { new LocalizationTextParam("count", "3") });

            StringAssert.Contains("3", text);
            StringAssert.DoesNotContain("{count}", text);
        }

        [Test]
        public void GetFormattedKeepsAndReportsPlaceholderWithoutValue()
        {
            Localize.Initialize();

            // 値の無い枠は残し、開発者ログへ出す
            // A placeholder without a value stays and is reported to the developer log
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"ui\.loading\.terrainReady.*\{elapsed\}"));
            var text = Localize.GetFormatted(LocalizationKeys.Ui.Loading.TerrainReady, new[] { new LocalizationTextParam("chunkCount", "3") });

            StringAssert.Contains("3", text);
            StringAssert.Contains("{elapsed}", text);
        }
    }
}
