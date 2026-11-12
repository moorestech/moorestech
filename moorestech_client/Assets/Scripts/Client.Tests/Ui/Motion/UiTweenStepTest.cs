using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Motion.Steps;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// 段階3以降で使う translate・scrollOffset の補間ステップが、進捗どおりの値を要素へ書くことを確かめる（L1）
    /// Verifies the translate and scrollOffset tween steps used from stage 3 write values that follow the progress onto the element (L1)
    /// </summary>
    public class UiTweenStepTest : UiTestHost
    {
        [TestCase(0f, 40f)]
        [TestCase(0.5f, 20f)]
        [TestCase(1f, 0f)]
        [TestCase(1.25f, -10f)]
        public void TranslateYは縦だけを補間し横は0に置く(float progress, float expectedY)
        {
            var target = new VisualElement();
            new TranslateYTweenStep(target, 40f, 0f).Apply(progress);

            var translate = target.style.translate.value;
            Assert.AreEqual(0f, translate.x.value);
            Assert.AreEqual(LengthUnit.Pixel, translate.y.unit);
            Assert.AreEqual(expectedY, translate.y.value, 0.001f);
        }

        [TestCase(0f, -12f)]
        [TestCase(0.5f, -6f)]
        [TestCase(1f, 0f)]
        [TestCase(1.25f, 3f)]
        public void TranslateXは横だけを補間し縦は0に置く(float progress, float expectedX)
        {
            var target = new VisualElement();
            new TranslateXTweenStep(target, -12f, 0f).Apply(progress);

            var translate = target.style.translate.value;
            Assert.AreEqual(0f, translate.y.value);
            Assert.AreEqual(LengthUnit.Pixel, translate.x.unit);
            Assert.AreEqual(expectedX, translate.x.value, 0.001f);
        }

        [Test]
        public void ScrollOffsetは進捗どおりにスクロール位置を書く()
        {
            // スクロール位置は中身が枠より大きいときだけ効くので、パネルへ載せてレイアウトを確定させる
            // The scroll offset only applies when the content exceeds the viewport, so mount it on a panel and settle the layout
            var scrollView = new ScrollView(ScrollViewMode.Vertical) { style = { width = 100, height = 100 } };
            scrollView.Add(new VisualElement { style = { width = 100, height = 1000 } });
            rootVisualElement.Add(scrollView);
            simulate.FrameUpdate();
            var step = new ScrollOffsetTweenStep(scrollView, new Vector2(0f, 0f), new Vector2(0f, 400f));

            step.Apply(0.5f);
            Assert.AreEqual(200f, scrollView.scrollOffset.y, 0.001f);
            step.Apply(1f);
            Assert.AreEqual(400f, scrollView.scrollOffset.y, 0.001f);
        }
    }
}
