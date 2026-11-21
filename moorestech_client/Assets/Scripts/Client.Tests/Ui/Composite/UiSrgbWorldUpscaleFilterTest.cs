using Client.Ui.Foundation.Composite;
using NUnit.Framework;
using UnityEngine.Rendering;
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
        public void 最終ポスト処理パスがあるときAutoは整数倍で割り切れる画面だけ点で読む()
        {
            Assert.IsTrue(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 0.5f, 1280, 720, UpscalingFilterSelection.Auto));
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 0.5f, 1281, 720, UpscalingFilterSelection.Auto), "odd width is not an integer scale");
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 0.75f, 1280, 720, UpscalingFilterSelection.Auto), "1/0.75 is not an integer");
        }

        [Test]
        public void 最終ポスト処理パスがあるとき拡大しないときと明示の選択はその通りに読む()
        {
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 1f, 1280, 720, UpscalingFilterSelection.Point), "no upscale");
            Assert.IsTrue(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 0.75f, 1280, 720, UpscalingFilterSelection.Point));
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 0.5f, 1280, 720, UpscalingFilterSelection.Linear));
        }

        [Test]
        public void 最終ポスト処理パスが無いときは常に双線形で読む()
        {
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, false, false, 0.5f, 1280, 720, UpscalingFilterSelection.Auto));
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, false, false, 0.5f, 1280, 720, UpscalingFilterSelection.Point));
        }

        [Test]
        public void ポスト処理が無効なカメラは常に双線形で読む()
        {
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(false, true, false, 0.5f, 1280, 720, UpscalingFilterSelection.Auto));
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(false, true, false, 0.5f, 1280, 720, UpscalingFilterSelection.Point));
        }

        [Test]
        public void STPは対応環境ならポスト処理の中で拡大し終え非対応ならAutoとして読む()
        {
            // 対応環境では拡大が残らないので双線形、非対応では Auto と同じ判定になる
            // Supported STP leaves no upscale (bilinear); unsupported STP follows the Auto rule
            var expected = !STP.IsSupported();
            Assert.AreEqual(expected, UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 0.5f, 1280, 720, UpscalingFilterSelection.STP));
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, true, false, 0.5f, 1281, 720, UpscalingFilterSelection.STP));
        }

        [Test]
        public void TAAのCASがあると拡大中は双線形で読む()
        {
            // URP は CAS で鋭くするので合成では写せず、双線形で読んで 1 回エラーを出す
            // URP sharpens with CAS, which the composite cannot reproduce, so it reads bilinearly and logs once
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsFalse(UiSrgbWorldUpscaleFilter.UsesPointSampling(true, false, true, 0.5f, 1280, 720, UpscalingFilterSelection.Point));
        }
    }
}
