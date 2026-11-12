using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Controls.GameScrollViewAxis
{
    /// <summary>
    /// GameScrollView の横向き（越えたときだけ流れの中に横のバー・縦のバーは出さない・太さと色は縦と同じつまみ）
    /// GameScrollView horizontal mode (an in-flow horizontal bar only on overflow, never a vertical bar, the same knobs for size and colors)
    /// </summary>
    public class GameScrollViewHorizontalTest : UiTestHost
    {
        private GameScrollView _view;
        private VisualElement _last;

        [SetUp]
        public void MountRowThatFits()
        {
            // 幅 300 の親に幅 80・高さ 36 の箱を 3 つ（計 240 で収まる）
            // Three 80x36 boxes in a 300-wide parent (240 in total, so they fit)
            var host = new VisualElement();
            host.style.width = 300;
            _view = new GameScrollView("probe-horizontal-scroll", GameScrollAxis.Horizontal);
            for (var i = 0; i < 3; i++) AddBox();
            host.Add(_view);
            rootVisualElement.Add(host);
            Settle();
        }

        [Test]
        public void 横向きは収まるときバーを出さず高さは中身の高さ()
        {
            Assert.AreEqual(ScrollViewMode.Horizontal, _view.mode);
            Assert.IsTrue(_view.ClassListContains("game-scroll-view--horizontal"));
            Assert.AreEqual(DisplayStyle.None, _view.horizontalScroller.resolvedStyle.display, "横のバー");
            Assert.AreEqual(DisplayStyle.None, _view.verticalScroller.resolvedStyle.display, "縦のバー");
            Assert.AreEqual(36f, _view.worldBound.height, 0.5f);
        }

        [Test]
        public void 越えると本文の下に横のバーが出て末尾まで送れ縦のバーは出さない()
        {
            // 箱を 6 つ（計 480）にして幅 300 を越えさせる
            // Six boxes (480 in total) overflow the 300 width
            for (var i = 0; i < 3; i++) AddBox();
            Settle();
            var scroller = _view.horizontalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");

            // バーは本文の下の流れの中で太さ 12。本文の高さを削らず、全体が中身 36＋バー 12 になる
            // The bar sits in the flow below the body, 12 thick; the body keeps its height and the whole is 36 + 12
            Assert.AreEqual(DisplayStyle.Flex, scroller.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, _view.verticalScroller.resolvedStyle.display);
            Assert.AreEqual(12f, scroller.worldBound.height, 0.01f);
            Assert.AreEqual(_view.contentViewport.worldBound.yMax, scroller.worldBound.yMin, 0.5f);
            Assert.AreEqual(36f + 12f, _view.worldBound.height, 0.5f);

            // 溝とつまみは縦と同じつまみ（gauge-track・角なしの bevel-c2）。つまみは内側 2px（整数丸め）を空けた太さ 8
            // Track and thumb use the vertical knobs (gauge-track, square bevel-c2); the thumb is 8 thick inside the 2px (snapped) inset
            AssertColor(Rgba(10, 14, 27, 0.8f), scroller.resolvedStyle.backgroundColor, "track");
            AssertColor(Rgba(104, 106, 120, 1f), dragger.resolvedStyle.backgroundColor, "thumb");
            Assert.AreEqual(8f, dragger.worldBound.height, 0.5f, "つまみの太さ");
            Assert.Less(dragger.worldBound.width, scroller.worldBound.width, "つまみの長さが溝より短い");

            // 末尾の箱まで送れる（Web overflow-x: auto）
            // The last box scrolls into view (Web overflow-x: auto)
            _view.ScrollTo(_last);
            Settle();
            Assert.Greater(_view.scrollOffset.x, 0f);
            Assert.LessOrEqual(_last.worldBound.xMax, _view.contentViewport.worldBound.xMax + 0.5f);
        }

        private void AddBox()
        {
            var box = new VisualElement();
            box.style.width = 80;
            box.style.height = 36;
            box.style.flexShrink = 0;
            _view.Add(box);
            _last = box;
        }

        private void Settle()
        {
            // バーの出し入れで 2 回目のレイアウトが走る
            // Showing or hiding the bar triggers a second layout
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }
    }
}
