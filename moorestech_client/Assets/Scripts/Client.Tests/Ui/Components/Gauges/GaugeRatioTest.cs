using Client.Ui.Components.Gauges;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// Clamp01 を Web と照らす
    /// Checks Clamp01 against the Web
    /// </summary>
    public class GaugeRatioTest
    {
        /// <web-test file="src/shared/clamp01.test.ts">負値は 0 に丸める</web-test>
        [Test]
        public void 負値は0に丸める()
        {
            Assert.AreEqual(0f, GaugeRatio.Clamp01(-0.5f));
        }

        /// <web-test file="src/shared/clamp01.test.ts">1 超は 1 に丸める</web-test>
        [Test]
        public void 一を超える値は1に丸める()
        {
            Assert.AreEqual(1f, GaugeRatio.Clamp01(2f));
        }

        /// <web-test file="src/shared/clamp01.test.ts">NaN は 0 にする</web-test>
        [Test]
        public void NaNは0にする()
        {
            Assert.AreEqual(0f, GaugeRatio.Clamp01(float.NaN));
        }

        /// <web-test file="src/shared/clamp01.test.ts">範囲内はそのまま返す</web-test>
        [Test]
        public void 範囲内はそのまま返す()
        {
            Assert.AreEqual(0.4f, GaugeRatio.Clamp01(0.4f));
        }
    }
}
