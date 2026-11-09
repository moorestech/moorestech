using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Progress;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Progress
{
    /// <summary>
    /// 採掘進捗がホットバーの床の 12px 上に全幅 1 本で出て、進捗と出し入れに従うことを確かめる（L1。720p・2560×1080 相当・縮小 960×540〈倍率 0.75〉）
    /// Verifies the mining progress is a single full-width gauge 12px above the hotbar floor and follows progress and visibility (L1; 720p and the 2560x1080 equivalent)
    /// </summary>
    [TestFixture(1280f, 720f, 1f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    [TestFixture(1280f, 720f, 0.75f)]
    public class ProgressHudViewTest : UiTestHost
    {
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;
        private ProgressHudView _view;

        public ProgressHudViewTest(float width, float height, float panelPixelsPerPoint)
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
        public void MountView()
        {
            pixelsPerPoint = _pixelsPerPoint;
            var stack = UiLayerStack.Build(rootVisualElement);
            _view = new ProgressHudView();
            stack.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            _view.SetShown(true);
            _view.SetProgress(0.5f);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/progress.spec.ts">採掘進捗は対象名を出さずホットバー全幅の12px上に1本だけ描画する</web-test>
        [Test]
        public void 採掘進捗は対象名を出さずホットバー全幅の12px上に1本だけ描画する()
        {
            var column = _view.Q("progress-bar");
            var floor = ThemeTokenFile.ResolvePixels("--hotbar-floor-offset");
            var gap = ThemeTokenFile.ResolvePixels("--progress-hotbar-gap");

            // ゲージは 1 本、文字は無い。幅はホットバー全幅、下端は床＋12px
            // One gauge and no text; width is the full hotbar width and the bottom is the floor plus 12px
            Assert.AreEqual(1, column.Query<GaugeBar>().ToList().Count);
            Assert.AreEqual(0, column.Query<Label>().ToList().Count);
            Assert.AreEqual(12f, gap, 0.001f);
            Assert.AreEqual(SnapToDevicePixel(ExpectedColumnRight()) - SnapToDevicePixel(ExpectedColumnLeft()), column.worldBound.width, 0.05f);
            Assert.AreEqual(SnapToDevicePixel(_logicalPanelSize.y - floor - gap), column.worldBound.yMax, 0.05f);
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="e2e/tests/progress.spec.ts">縮小viewportでも採掘進捗とホットバーの相対配置を維持する</web-test>
        [Test]
        public void どの寸法でもホットバーと同じ幅と中心と隙間を物理pxで保つ()
        {
            var column = _view.Q("progress-bar");
            var offset = ThemeTokenFile.ResolvePixels("--hotbar-horizontal-offset");
            var width = ThemeTokenFile.ResolvePixels("--hotbar-width");
            var floor = ThemeTokenFile.ResolvePixels("--hotbar-floor-offset");
            var gap = ThemeTokenFile.ResolvePixels("--progress-hotbar-gap");
            var physicalHeight = _logicalPanelSize.y * _pixelsPerPoint;
            var physicalLeft = PhysicalRound((_logicalPanelSize.x / 2f + offset - width / 2f) * _pixelsPerPoint);
            var physicalRight = PhysicalRound((_logicalPanelSize.x / 2f + offset + width / 2f) * _pixelsPerPoint);

            // 物理 px は論理 px × 倍率。縮小 960×540 でも幅・中心・床からの隙間はトークン × 0.75 を物理画素へ寄せた値
            // Physical px are logical px times the scale; even at the reduced 960x540 width, center and floor gap are tokens times 0.75 snapped to device pixels
            Assert.AreEqual(physicalRight - physicalLeft, column.worldBound.width * _pixelsPerPoint, 0.1f);
            Assert.AreEqual((physicalLeft + physicalRight) / 2f, column.worldBound.center.x * _pixelsPerPoint, 0.1f);
            Assert.AreEqual(physicalHeight - PhysicalRound(physicalHeight - (floor + gap) * _pixelsPerPoint), physicalHeight - column.worldBound.yMax * _pixelsPerPoint, 0.1f);
        }

        // UITK は辺を物理画素へ半分切り上げで寄せる（Mathf.Round は偶数丸めなので使わない）。幅は寄せた両辺の差になる
        // UITK snaps edges half-up to device pixels (not Mathf.Round, which rounds half to even); the width is the difference of the snapped edges
        private static float PhysicalRound(float physical)
        {
            return Mathf.Floor(physical + 0.5f);
        }

        private float SnapToDevicePixel(float logical)
        {
            return PhysicalRound(logical * _pixelsPerPoint) / _pixelsPerPoint;
        }

        private float ExpectedColumnLeft()
        {
            return _logicalPanelSize.x / 2f + ThemeTokenFile.ResolvePixels("--hotbar-horizontal-offset") - ThemeTokenFile.ResolvePixels("--hotbar-width") / 2f;
        }

        private float ExpectedColumnRight()
        {
            return ExpectedColumnLeft() + ThemeTokenFile.ResolvePixels("--hotbar-width");
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">採掘進捗・クロスヘア・tooltipのtopic eventを表示する</web-test>
        [Test]
        public void 進捗と出し入れに従い充填は白で入力を奪わない()
        {
            var gauge = _view.Q<GaugeBar>("progress-gauge");
            var fill = gauge.Q(className: "gauge-bar__fill");

            // 充填の幅は進捗の割合、色は採掘 HUD だけの白（--color-content-primary）
            // The fill width is the progress ratio and its color is the mining-only white (--color-content-primary)
            Assert.AreEqual(0.5f, fill.worldBound.width / gauge.worldBound.width, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--color-content-primary"), fill.resolvedStyle.backgroundColor, "fill");
            foreach (var element in _view.Query<VisualElement>().ToList()) Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name);

            _view.SetShown(false);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, _view.Q("progress-bar").resolvedStyle.display);
        }
    }
}
