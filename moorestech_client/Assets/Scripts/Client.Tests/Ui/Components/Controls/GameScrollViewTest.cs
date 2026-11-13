// moorestech_client/Assets/Scripts/Client.Tests/Ui/Components/Controls/GameScrollViewTest.cs
using System.Collections.Generic;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// バーの重なり・色・寸法の確認
    /// Checks bar overlay, colors and sizes
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
            _view = new GameScrollView("probe-scroll", GameScrollAxis.Vertical);
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
            // 余白は style に 2.4px で入り、UI Toolkit が整数ピクセルへ丸める。実測は x=2・幅8（Web は 2.4・7.2 で記帳済みの差）
            // Inset is 2.4px in style and UI Toolkit snaps layout to whole pixels; measured x=2, width=8 (known gap from Web 2.4/7.2)
            Assert.AreEqual(2.4f, scroller.style.paddingLeft.value.value, 0.01f);
            Assert.AreEqual(2f, dragger.worldBound.x - scroller.worldBound.x, 0.01f);
            Assert.AreEqual(8f, dragger.worldBound.width, 0.01f);
        }

        [Test]
        public void 矢印ボタンは出さず溝はバーの全高に張る()
        {
            var scroller = _view.verticalScroller;
            var slider = scroller.Q(className: "unity-scroller__slider");

            Assert.AreEqual(DisplayStyle.None, scroller.Q(className: "unity-scroller__low-button").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, scroller.Q(className: "unity-scroller__high-button").resolvedStyle.display);
            Assert.AreEqual(200f, scroller.worldBound.height, 0.01f);
            // 溝は上下に内側 2px（整数丸め）だけ空けてバー全高に張る
            // The track spans the full bar height minus the 2px (snapped) inset on both ends
            Assert.AreEqual(2f, slider.worldBound.y - scroller.worldBound.y, 0.01f);
            Assert.AreEqual(196f, slider.worldBound.height, 0.01f);
        }

        [Test]
        public void つまみは先頭で溝の上端にありスクロール量に比例して下がる()
        {
            var scroller = _view.verticalScroller;
            var slider = scroller.Q(className: "unity-scroller__slider");
            var dragger = scroller.Q(className: "unity-base-slider__dragger");
            Assert.AreEqual(slider.worldBound.y, dragger.worldBound.y, 0.5f);

            // 末尾ではつまみの下端が溝の下端に付く
            // At the end the thumb bottom touches the track bottom
            _view.SetScrollOffset(scroller.highValue);
            Settle();
            Assert.AreEqual(slider.worldBound.yMax, dragger.worldBound.yMax, 0.5f);
        }

        [Test]
        public void 溝はgauge_trackでつまみは角なしのbevel_c2()
        {
            var scroller = _view.verticalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");

            AssertColor(Rgba(10, 14, 27, 0.8f), scroller.resolvedStyle.backgroundColor, "track");
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
            // 実測: 4px 上書き時は x=1・幅2（0.8 と 2.4 の丸め）
            // Measured at the 4px override: x=1, width=2 (snapped from 0.8 and 2.4)
            Assert.AreEqual(1f, dragger.worldBound.x - scroller.worldBound.x, 0.01f);
            Assert.AreEqual(2f, dragger.worldBound.width, 0.01f);
        }

        [Test]
        public void ホイール1ノッチで本文がWebと同じ60px動く()
        {
            // 1 ノッチは WheelEvent の scrollDeltaPerTick 行（InputSystem の 1 ノッチと同じ量）
            // One notch is scrollDeltaPerTick lines of WheelEvent, the amount InputSystem delivers per notch
            simulate.ScrollWheel(new Vector2(0f, WheelEvent.scrollDeltaPerTick), _view.contentViewport.worldBound.center);
            Settle();

            // 正本は CefUnity の ScrollInputPipeline.WheelPixelsPerStep（Tests から参照できないためリテラルで照合）
            // The source of truth is CefUnity's ScrollInputPipeline.WheelPixelsPerStep (not referenceable from Tests, so compared as a literal)
            Assert.AreEqual(60f, _view.scrollOffset.y, 0.5f);
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
