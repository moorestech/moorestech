using Client.Ui.Foundation.Composite;
using NUnit.Framework;
using UnityEngine.Rendering.Universal;

namespace Client.Tests.Ui.Composite
{
    /// <summary>
    /// 世界の色の拡大フィルタが URP の選び方と同じになる
    /// The world colour upscale filter follows URP's selection
    /// </summary>
    public class UiSrgbWorldUpscaleFilterTest
    {
        [Test]
        public void Autoは整数倍で割り切れる画面だけ点で読む()
        {
            Assert.IsTrue(UiSrgbWorldUpscaleFilter.UsesPointSampling(0.5f, 1280, 720, UpscalingFilterSelection.Auto));
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(0.5f, 1281, 720, UpscalingFilterSelection.Auto), "odd width is not an integer scale");
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(0.75f, 1280, 720, UpscalingFilterSelection.Auto), "1/0.75 is not an integer");
        }

        [Test]
        public void 拡大しないときと明示の選択はその通りに読む()
        {
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(1f, 1280, 720, UpscalingFilterSelection.Point), "no upscale");
            Assert.IsTrue(UiSrgbWorldUpscaleFilter.UsesPointSampling(0.75f, 1280, 720, UpscalingFilterSelection.Point));
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(0.5f, 1280, 720, UpscalingFilterSelection.Linear));
        }
    }
}
