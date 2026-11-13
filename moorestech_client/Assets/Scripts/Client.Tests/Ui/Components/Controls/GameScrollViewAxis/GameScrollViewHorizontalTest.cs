using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Controls.GameScrollViewAxis
{
    /// <summary>
    /// GameScrollView の横向き
    /// GameScrollView horizontal mode
    /// </summary>
    public class GameScrollViewHorizontalTest : UiTestHost
    {
        private GameScrollView _view;
        private VisualElement _last;

        [SetUp]
        public void MountRowThatFits()
        {
            // 幅 300 の親に幅 80 の箱 3 つ
            // Three 80-wide boxes in a 300-wide parent
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
            // 箱 6 つで幅 300 を越える
            // Six boxes overflow the 300 width
            for (var i = 0; i < 3; i++) AddBox();
            Settle();
            var scroller = _view.horizontalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");

            // バーは本文の下で太さ 12
            // The bar sits below the body, 12 thick
            Assert.AreEqual(DisplayStyle.Flex, scroller.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, _view.verticalScroller.resolvedStyle.display);
            Assert.AreEqual(12f, scroller.worldBound.height, 0.01f);
            Assert.AreEqual(_view.contentViewport.worldBound.yMax, scroller.worldBound.yMin, 0.5f);
            Assert.AreEqual(36f + 12f, _view.worldBound.height, 0.5f);

            // 溝・つまみは縦と同じ（太さ 8）
            // Track and thumb match vertical (8 thick)
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

        [Test]
        public void 横向きは縦ホイールで送らず横の量の1ノッチで60px送る()
        {
            // 箱 6 つで幅 300 を越える
            // Six boxes overflow the 300 width
            for (var i = 0; i < 3; i++) AddBox();
            Settle();
            var center = _view.contentViewport.worldBound.center;

            // 縦ノッチでは横が動かない（Web overflow-x: auto）
            // A vertical notch leaves horizontal unchanged (Web overflow-x: auto)
            simulate.ScrollWheel(new Vector2(0f, WheelEvent.scrollDeltaPerTick), center);
            Settle();
            Assert.AreEqual(0f, _view.scrollOffset.x, 0.01f, "縦ホイール");

            // 横 1 ノッチで 60px 送る
            // One horizontal notch scrolls 60px
            simulate.ScrollWheel(new Vector2(WheelEvent.scrollDeltaPerTick, 0f), center);
            Settle();
            Assert.AreEqual(60f, _view.scrollOffset.x, 0.01f, "横ホイール");
            Assert.AreEqual(0f, _view.scrollOffset.y, 0.01f);
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
