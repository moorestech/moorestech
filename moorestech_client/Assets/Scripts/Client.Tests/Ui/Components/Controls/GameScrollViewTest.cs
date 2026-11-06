// moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Controls/GameScrollViewTest.cs
using System.Collections.Generic;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// GameScrollView のバーが Mantine ScrollArea と同じく本文に重なり、panelBodyScrollArea の色と寸法で描かれることを確かめる
    /// Verifies the GameScrollView bar overlays the body like Mantine ScrollArea with panelBodyScrollArea colors and sizes
    /// </summary>
    public class GameScrollViewTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/ComponentTokenProbe.uss";
        private VisualElement _host;
        private GameScrollView _view;
        private VisualElement _last;

        [SetUp]
        public void MountTallContent()
        {
            _host = new VisualElement();
            _host.style.width = 300;
            _host.style.height = 200;
            _view = new GameScrollView("probe-scroll");
            for (var i = 0; i < 20; i++)
            {
                var row = new VisualElement();
                row.style.height = 40;
                _view.Add(row);
                _last = row;
            }
            _host.Add(_view);
            rootVisualElement.Add(_host);
            Settle();
        }

        [Test]
        public void 縦だけのスクロールで本文を親いっぱいに伸ばす()
        {
            Assert.AreEqual("probe-scroll", _view.name);
            Assert.AreEqual(ScrollViewMode.Vertical, _view.mode);
            Assert.AreEqual(ScrollerVisibility.Hidden, _view.horizontalScrollerVisibility);
            Assert.AreEqual(200f, _view.worldBound.height, 0.01f);
            Assert.Greater(_view.contentContainer.layout.height, _view.contentViewport.layout.height);
        }

        [Test]
        public void バーは本文の幅を削らず右端に重なり幅12pxでつまみは内側2_4px()
        {
            var scroller = _view.verticalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");

            Assert.AreEqual(DisplayStyle.Flex, scroller.resolvedStyle.display);
            Assert.AreEqual(300f, _view.contentViewport.worldBound.width, 0.01f);
            Assert.AreEqual(_view.worldBound.xMax, scroller.worldBound.xMax, 0.01f);
            Assert.AreEqual(12f, scroller.worldBound.width, 0.01f);
            // 余白の値は style に 2.4px で入り、レイアウトは UI Toolkit が整数ピクセルへ丸める（2.4 → 2、7.2 → 8 に近い値）
            // The inset lands in style as 2.4px while UI Toolkit snaps layout to whole pixels (2.4 -> 2, thumb 7.2 -> about 8)
            Assert.AreEqual(2.4f, scroller.style.paddingLeft.value.value, 0.01f);
            Assert.AreEqual(2.4f, dragger.worldBound.x - scroller.worldBound.x, 0.5f);
            Assert.AreEqual(7.2f, dragger.worldBound.width, 1f);
        }

        [Test]
        public void 溝はgauge_trackでつまみは角なしのbevel_c2()
        {
            var scroller = _view.verticalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");

            AssertColor(Rgba(10, 14, 27, 0.971f), scroller.resolvedStyle.backgroundColor, "track");
            AssertColor(Rgba(104, 106, 120, 1f), dragger.resolvedStyle.backgroundColor, "thumb");
            Assert.AreEqual(0f, dragger.resolvedStyle.borderTopLeftRadius, 0.001f);
        }

        [Test]
        public void 本文が収まるときはバーを出さない()
        {
            _view.Clear();
            _view.Add(new VisualElement { style = { height = 40 } });
            Settle();

            Assert.AreEqual(DisplayStyle.None, _view.verticalScroller.resolvedStyle.display);
        }

        [Test]
        public void SetScrollOffsetは位置を動かし変化を通知する()
        {
            var offsets = new List<float>();
            _view.OnScrollOffsetChanged.Subscribe(offsets.Add);

            _view.SetScrollOffset(100f);
            Settle();
            Assert.AreEqual(100f, _view.scrollOffset.y, 0.01f);
            CollectionAssert.Contains(offsets, 100f);
        }

        [Test]
        public void ScrollToは末尾の子を見える位置へ動かす()
        {
            _view.ScrollTo(_last);
            Settle();

            Assert.LessOrEqual(_last.worldBound.yMax, _view.contentViewport.worldBound.yMax + 0.5f);
            Assert.GreaterOrEqual(_last.worldBound.y, _view.contentViewport.worldBound.y - 0.5f);
        }

        [Test]
        public void 祖先でバーの幅を上書きすると幅と内側が比で変わる()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            _host.AddToClassList("component-knob-probe");
            Settle();

            var scroller = _view.verticalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");
            Assert.AreEqual(4f, scroller.worldBound.width, 0.01f);
            Assert.AreEqual(0.8f, scroller.style.paddingLeft.value.value, 0.01f);
            Assert.AreEqual(0.8f, dragger.worldBound.x - scroller.worldBound.x, 0.5f);
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
