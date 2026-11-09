using Client.Ui.Components.Slots;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Slots.Fluid
{
    /// <summary>
    /// 液体の量と容量からフィルの割合を出す規則（Web の fillRatio）を確かめる（L0）
    /// Verifies the fill ratio rule from fluid amount and capacity (the Web fillRatio) (L0)
    /// </summary>
    public class FluidFillRatioTest
    {
        /// <web-test file="src/shared/ui/FluidSlot/fluidLogic.test.ts">半量は 0.5</web-test>
        [Test]
        public void 半量は0_5()
        {
            Assert.AreEqual(0.5f, FluidFillRatio.Of(500, 1000), 1e-6f);
        }

        /// <web-test file="src/shared/ui/FluidSlot/fluidLogic.test.ts">capacity 0 は 0</web-test>
        [Test]
        public void 容量0は0()
        {
            Assert.AreEqual(0f, FluidFillRatio.Of(500, 0), 1e-6f);
        }

        /// <web-test file="src/shared/ui/FluidSlot/fluidLogic.test.ts">超過は 1 にクランプ</web-test>
        [Test]
        public void 超過は1に丸める()
        {
            Assert.AreEqual(1f, FluidFillRatio.Of(1500, 1000), 1e-6f);
        }

        [Test]
        public void 割合がNaNなら0として描く()
        {
            // Web の clamp01 は NaN を 0 にする
            // The Web clamp01 turns NaN into 0
            Assert.AreEqual(0f, FluidFillRatio.Of(double.NaN, 1000), 1e-6f);
        }
    }
}
