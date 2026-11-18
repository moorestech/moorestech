using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// GameScrollView の追加つまみ（溝・つまみの色・縦バーの位置・内側余白の比）の既定と上書きの効き（L1）
    /// Defaults and overrides of the extra GameScrollView knobs (track/thumb colors, bar insets, padding ratio) (L1)
    /// </summary>
    public class GameScrollViewKnobTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Controls/ControlKnobOverrides.uss";
        private VisualElement _host;

        [SetUp]
        public void MountHost()
        {
            // 縦バーが出るように親の高さを決め、プローブの USS を根に当てる
            // Fix the host height so the bar shows, and put the probe USS on the root
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            _host = new VisualElement();
            _host.style.width = 300;
            _host.style.height = 200;
            rootVisualElement.Add(_host);
        }

        [Test]
        public void つまみの既定は今の値で上書きすると溝つまみの色とバーの位置が変わる()
        {
            var scroll = MountTall(_host, "knob");
            var scroller = scroll.verticalScroller;
            var dragger = scroller.Q(className: "unity-base-slider__dragger");
            AssertBarInsets(scroll, 0f, 0f, 0f);
            AssertColor(Rgba(10, 14, 27, 0.8f), scroller.resolvedStyle.backgroundColor, "default track");
            AssertColor(Rgba(104, 106, 120, 1f), dragger.resolvedStyle.backgroundColor, "default thumb");

            // 部品自身のクラスで上書きしても効く
            // Overriding on the component's own element also takes effect
            scroll.AddToClassList("game-scroll-view-knob-probe");
            Settle();
            // resolvedStyle の right・bottom は絶対配置で符号が揺れるので、置かれた位置で見る
            // resolvedStyle right/bottom flip sign under absolute positioning, so check the laid-out position
            AssertBarInsets(scroll, 12f, 12f, 12f);
            AssertColor(new Color(0, 0, 0, 0), scroller.resolvedStyle.backgroundColor, "track");
            AssertColor(Color.white, dragger.resolvedStyle.backgroundColor, "thumb");
        }

        [Test]
        public void 内側余白の比のつまみでつまみ本体の幅が変わる()
        {
            // 既定の比 0.2・幅 4 では内側 0.8 がレイアウトで丸まり、つまみは 2（LX-42）
            // At the default ratio 0.2 and width 4 the 0.8 padding snaps in layout, leaving a 2px thumb (LX-42)
            _host.AddToClassList("game-scroll-view-size-probe");
            var scroll = MountTall(_host, "ratio");
            var dragger = scroll.verticalScroller.Q(className: "unity-base-slider__dragger");
            Assert.AreEqual(0.8f, scroll.verticalScroller.style.paddingLeft.value.value, 0.01f);
            Assert.AreEqual(2f, dragger.worldBound.width, 0.01f);

            // 比 0 ではつまみ本体がバーの幅いっぱい
            // With ratio 0 the thumb spans the full bar width
            _host.AddToClassList("game-scroll-view-ratio-probe");
            Settle();
            Assert.AreEqual(0f, scroll.verticalScroller.style.paddingLeft.value.value, 0.001f);
            Assert.AreEqual(4f, scroll.verticalScroller.worldBound.width, 0.01f);
            Assert.AreEqual(4f, dragger.worldBound.width, 0.01f);
        }

        [Test]
        public void 祖先の要素で上書きしても中のGameScrollViewのバーの位置が変わる()
        {
            // 画面は部品の外側の要素（RecipeListScrollView の外枠など）でつまみを上書きするので、継承で中まで届くことを見る
            // Screens override the knobs on an outer element (e.g. the RecipeListScrollView shell), so check it inherits down to the inner view
            var outer = new VisualElement();
            outer.style.flexGrow = 1;
            outer.AddToClassList("game-scroll-view-knob-probe");
            _host.Add(outer);
            var scroll = MountTall(outer, "knob-inner");
            AssertBarInsets(scroll, 12f, 12f, 12f);
        }

        private static void AssertBarInsets(GameScrollView scroll, float top, float right, float bottom)
        {
            var bar = scroll.verticalScroller.worldBound;
            Assert.AreEqual(top, bar.y - scroll.worldBound.y, 0.01f, "top");
            Assert.AreEqual(right, scroll.worldBound.xMax - bar.xMax, 0.01f, "right");
            Assert.AreEqual(bottom, scroll.worldBound.yMax - bar.yMax, 0.01f, "bottom");
        }

        private GameScrollView MountTall(VisualElement parent, string testid)
        {
            // 視口 200 に対して中身 800 で溢れさせる
            // Overflow with 800 of content against a 200 viewport
            var scroll = new GameScrollView(testid, GameScrollAxis.Vertical);
            for (var i = 0; i < 20; i++) scroll.Add(new VisualElement { style = { height = 40 } });
            parent.Add(scroll);
            Settle();
            return scroll;
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
