using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// GamePanel（craft）の面・内周の 2 本・グリップ・余白が Web の .craft と同じかを確かめる（L1）
    /// Verifies the GamePanel (craft) face, two inset rings, grip and padding match the Web .craft (L1)
    /// </summary>
    public class GamePanelCraftTest : UiTestHost
    {
        // Web の border: 1px solid transparent
        // The Web border: 1px solid transparent
        private const float TransparentBorder = 1f;

        // Web の .craft の inset 0 0 0 3.132px（暗い線）と 3.524px（明るい輪）、その値を整数の物理画素にする Web の倍率
        // The Web .craft inset 0 0 0 3.132px (dark line) and 3.524px (light ring), and the Web scale that makes them whole device pixels
        private const float WebLineWidth = 3.132f;
        private const float WebRimWidth = 3.524f;
        private const float WebDevicePixelRatio = 10f / 3.913894f;

        private GamePanel _panel;

        [SetUp]
        public void AddPanel()
        {
            _panel = new GamePanel("test-craft-panel", GamePanelFrame.Craft);
            _panel.style.width = 288;
            _panel.style.height = 120;
            rootVisualElement.Add(_panel);
            simulate.FrameUpdate();
        }

        [Test]
        public void craftは面と内周の2本とグリップを持ちdefaultの面と三角を持たない()
        {
            Assert.IsTrue(_panel.ClassListContains("game-panel--craft"));
            Assert.IsNotNull(_panel.Q(className: "game-panel__craft-face"));
            Assert.IsNotNull(_panel.Q(className: "game-panel__craft-rim"));
            Assert.IsNotNull(_panel.Q(className: "game-panel__craft-line"));
            Assert.IsNotNull(_panel.Q(className: "game-panel__craft-grip"));
            Assert.IsNull(_panel.Q(className: "game-panel__face"));
            Assert.IsNull(_panel.Q(className: "game-panel__bottom-deco"));
        }

        [Test]
        public void 面は外寸いっぱいで暗い線は透明の1px枠の内側から始まる()
        {
            var panel = _panel.worldBound;
            var face = _panel.Q(className: "game-panel__craft-face").worldBound;
            var line = _panel.Q(className: "game-panel__craft-line").worldBound;
            Assert.AreEqual(panel.x, face.x, 0.5f);
            Assert.AreEqual(panel.xMax, face.xMax, 0.5f);
            Assert.AreEqual(panel.yMax, face.yMax, 0.5f);
            Assert.AreEqual(panel.x + TransparentBorder, line.x, 0.5f);
            Assert.AreEqual(panel.yMax - TransparentBorder, line.yMax, 0.5f);
        }

        // 1280×720・1920×1080・2560×1440 の倍率と、Web の 3.913894px＝10 物理画素から逆算した Web の倍率
        // Scales of 1280x720, 1920x1080 and 2560x1440, plus the Web scale derived from 3.913894px = 10 device pixels
        [TestCase(1f)]
        [TestCase(1.5f)]
        [TestCase(2f)]
        [TestCase(WebDevicePixelRatio)]
        public void 明るい輪は倍率によらず暗い線のすぐ内側に1物理画素だけ見える(float scale)
        {
            pixelsPerPoint = scale;
            simulate.FrameUpdate();
            var line = _panel.Q(className: "game-panel__craft-line");
            var rim = _panel.Q(className: "game-panel__craft-rim");

            // 暗い線は Web の 3.132px を物理画素へ丸めた幅で、輪はその内縁から 1 物理画素
            // The dark line is the Web 3.132px snapped to device pixels; the ring is one device pixel from its inner edge
            var lineWidth = Mathf.Round(WebLineWidth * scale) / scale;
            Assert.AreEqual(lineWidth, line.resolvedStyle.borderLeftWidth, 0.01f);
            Assert.AreEqual(line.worldBound.x + lineWidth, rim.worldBound.x, 0.01f);
            Assert.AreEqual(line.worldBound.yMax - lineWidth, rim.worldBound.yMax, 0.01f);
            Assert.AreEqual(1f / scale, rim.resolvedStyle.borderLeftWidth, 0.01f);
            Assert.AreEqual(1f / scale, rim.resolvedStyle.borderBottomWidth, 0.01f);
        }

        [Test]
        public void Webの倍率では暗い線と見える輪の幅がWebの値そのもの()
        {
            pixelsPerPoint = WebDevicePixelRatio;
            simulate.FrameUpdate();
            Assert.AreEqual(WebLineWidth, _panel.Q(className: "game-panel__craft-line").resolvedStyle.borderLeftWidth, 0.01f);
            Assert.AreEqual(WebRimWidth - WebLineWidth, _panel.Q(className: "game-panel__craft-rim").resolvedStyle.borderLeftWidth, 0.01f);
        }

        [Test]
        public void 本文はWebのpaddingと1pxの枠の内側に置かれる()
        {
            var panel = _panel.worldBound;
            var body = _panel.Q(className: "game-panel__body").worldBound;
            Assert.AreEqual(panel.x + TransparentBorder + 10f, body.x, 0.5f);
            Assert.AreEqual(panel.xMax - TransparentBorder - 15f, body.xMax, 0.5f);
            Assert.AreEqual(panel.yMax - TransparentBorder - 15f, body.yMax, 0.5f);
        }

        [Test]
        public void グリップは右下からinsetの位置にsizeの正方形で本文より前面()
        {
            var inset = ThemeTokenFile.ResolvePixels("--craft-grip-inset");
            var size = ThemeTokenFile.ResolvePixels("--craft-grip-size");
            var panel = _panel.worldBound;
            var grip = _panel.Q(className: "game-panel__craft-grip");
            Assert.AreEqual(panel.xMax - TransparentBorder - inset, grip.worldBound.xMax, 0.5f);
            Assert.AreEqual(panel.yMax - TransparentBorder - inset, grip.worldBound.yMax, 0.5f);
            Assert.AreEqual(size, grip.worldBound.width, 0.5f);
            var body = _panel.Q(className: "game-panel__body");
            Assert.Greater(_panel.hierarchy.IndexOf(grip), _panel.hierarchy.IndexOf(body));
        }

        [Test]
        public void 装飾は拾わない()
        {
            foreach (var className in new[] { "game-panel__craft-face", "game-panel__craft-rim", "game-panel__craft-line", "game-panel__craft-grip" })
            {
                Assert.AreEqual(PickingMode.Ignore, _panel.Q(className: className).pickingMode, className);
            }
        }

        [Test]
        public void 描画でエラーを出さない()
        {
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
