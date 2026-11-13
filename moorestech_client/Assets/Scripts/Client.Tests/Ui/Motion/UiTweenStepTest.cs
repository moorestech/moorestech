using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Motion.Steps;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Motion
{
    /// <summary>
    /// 補間ステップが進捗どおり値を書く（L1）
    /// Tween steps write progress values (L1)
    /// </summary>
    public class UiTweenStepTest : UiTestHost
    {
        [TestCase(0f, -12f)]
        [TestCase(0.5f, -6f)]
        [TestCase(1f, 0f)]
        [TestCase(1.25f, 3f)]
        public void TranslateXは横だけを補間し縦は現在値を保つ(float progress, float expectedX)
        {
            // 縦のずらしを先に置き、横の補間がそれを 0 へ戻さないことを見る
            // Set a vertical offset first and check the horizontal tween does not reset it to 0
            var target = new VisualElement();
            target.style.translate = new Translate(0, 7f);
            new TranslateXTweenStep(target, -12f, 0f).Apply(progress);

            var translate = target.style.translate.value;
            Assert.AreEqual(7f, translate.y.value, 0.001f);
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
