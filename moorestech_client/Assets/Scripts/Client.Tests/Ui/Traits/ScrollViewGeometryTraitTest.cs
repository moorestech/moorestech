using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Traits
{
    /// <summary>
    /// スクロール時の GeometryChanged を実測（L1）
    /// Measures GeometryChanged on scroll (L1)
    /// </summary>
    public class ScrollViewGeometryTraitTest : UiTestHost
    {
        [Test]
        public void スクロールだけでは中身にGeometryChangedEventが来ない()
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.height = 200;
            var heading = new VisualElement();
            heading.style.height = 1000;
            scrollView.Add(heading);
            rootVisualElement.Add(scrollView);
            simulate.FrameUpdate();

            var contentChanges = 0;
            var headingChanges = 0;
            scrollView.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => contentChanges++);
            heading.RegisterCallback<GeometryChangedEvent>(_ => headingChanges++);
            scrollView.scrollOffset = new Vector2(0f, 300f);
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // 仮説: スクロールは中身の平行移動で、レイアウトの矩形を変えない
            // Hypothesis: scrolling translates the contents and leaves their layout rect unchanged
            Assert.AreEqual(300f, scrollView.scrollOffset.y, 0.5f);
            Assert.AreEqual(0, contentChanges, "content GeometryChangedEvent on scroll");
            Assert.AreEqual(0, headingChanges, "child GeometryChangedEvent on scroll");
        }
    }
}
