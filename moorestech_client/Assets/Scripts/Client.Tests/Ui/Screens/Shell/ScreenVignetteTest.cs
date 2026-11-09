using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Shell
{
    /// <summary>
    /// ヴィネットが実画面の四辺へ沿い、縁の重みと濃さが Web の楕円グラデと同じことを確かめる（L1。720p・Web の 2432×786 相当・2560×1080 相当）
    /// Verifies the vignette spans every viewport edge and matches the Web elliptical gradient in weight and shade (L1; 720p and the Web 2432x786 equivalent)
    /// </summary>
    [TestFixture(1280f, 720f, 1f)]
    // 横幅は物理 2432px ÷ 1.0917。UITK は物理画素へ寄せるので、端数の論理幅だと要素の幅が 0.07 ずれる
    // Width is 2432 physical px / 1.0917; UITK snaps to physical pixels, so a rounded logical width drifts the element by 0.07
    [TestFixture(2227.7182f, 720f, 1.0917f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class ScreenVignetteTest : UiTestHost
    {
        // Web の tokens.css の --screen-vignette-edge の α（rgb(4 5 8 / 16%)）。補正の指数は直書きせず、トークンの α から逆に解く
        // Alpha of the Web tokens.css --screen-vignette-edge (rgb(4 5 8 / 16%)); the correction exponent is not hard-coded but solved back from the token alpha
        private const float WebEdgeAlpha = 0.16f;
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;

        public ScreenVignetteTest(float width, float height, float panelPixelsPerPoint)
        {
            _logicalPanelSize = new Vector2(width, height);
            _pixelsPerPoint = panelPixelsPerPoint;
        }

        [OneTimeSetUp]
        public void SetUpLogicalPanelSize()
        {
            panelSize = _logicalPanelSize;
        }

        [SetUp]
        public void SetUpPanelPixelsPerPoint()
        {
            pixelsPerPoint = _pixelsPerPoint;
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">横長画面でビネットを実viewportの四辺へ沿わせる</web-test>
        [Test]
        public void 横長画面でもビネットを実画面の四辺へ沿わせる()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            var view = new ScreenBackdropView();
            stack.GetLayer(UiLayer.Backdrop, UiFamily.Viewport).Add(view);
            simulate.FrameUpdate();

            // 実画面の族は論理パネル全面。描画でトークンの読み損ないの LogError が出ない
            // The viewport family is the whole logical panel; drawing raises no token-read LogError
            var vignette = view.Q("screen-vignette");
            Assert.AreEqual(0f, vignette.worldBound.x, 0.05f);
            Assert.AreEqual(0f, vignette.worldBound.y, 0.05f);
            Assert.AreEqual(_logicalPanelSize.x, vignette.worldBound.width, 0.05f);
            Assert.AreEqual(_logicalPanelSize.y, vignette.worldBound.height, 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ヴィネットの縁の重みと濃さはWebの楕円グラデと同じ()
        {
            Assert.IsTrue(UssTokenValue.TryParsePercent(ThemeTokenFile.Resolve("--screen-vignette-width"), out var widthRatio));
            Assert.IsTrue(UssTokenValue.TryParsePercent(ThemeTokenFile.Resolve("--screen-vignette-height"), out var heightRatio));
            Assert.IsTrue(UssTokenValue.TryParsePercent(ThemeTokenFile.Resolve("--screen-vignette-center-x"), out var centerX));
            Assert.IsTrue(UssTokenValue.TryParsePercent(ThemeTokenFile.Resolve("--screen-vignette-center-y"), out var centerY));
            Assert.IsTrue(UssTokenValue.TryParsePercent(ThemeTokenFile.Resolve("--screen-vignette-clear-stop"), out var clearStop));
            var shape = new ScreenVignetteShape(widthRatio, heightRatio, centerX, centerY, clearStop);
            var width = _logicalPanelSize.x;
            var height = _logicalPanelSize.y;
            var cx = width * centerX;
            var cy = height * centerY;
            var rx = width * widthRatio;
            var ry = height * heightRatio;

            // 中心と透明の停止点までは 0、停止点と楕円の中ほどで 0.5、楕円上と外は 1
            // Zero up to the clear stop, 0.5 halfway to the ellipse, one on and beyond the ellipse
            Assert.AreEqual(0f, shape.EdgeWeightAt(cx, cy, width, height), 1e-4f);
            Assert.AreEqual(0f, shape.EdgeWeightAt(cx + rx * clearStop, cy, width, height), 1e-4f);
            Assert.AreEqual(0.5f, shape.EdgeWeightAt(cx, cy + ry * (clearStop + 1f) / 2f, width, height), 1e-4f);
            Assert.AreEqual(1f, shape.EdgeWeightAt(cx + rx, cy, width, height), 1e-4f);
            Assert.AreEqual(1f, shape.EdgeWeightAt(cx - rx * 1.2f, cy, width, height), 1e-4f);

            // 縁のトークンは Web の α 0.16 のままで、重みを線形に掛ける（A-180）
            // The edge token keeps the Web alpha 0.16 and the weight is multiplied linearly (A-180)
            Assert.AreEqual(WebEdgeAlpha, ThemeTokenFile.ResolveColor("--screen-vignette-edge").a, 1e-4f);
        }
    }
}
