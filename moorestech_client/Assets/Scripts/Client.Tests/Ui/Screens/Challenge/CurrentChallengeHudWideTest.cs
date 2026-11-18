using Client.Tests.Ui.Components;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.State.Challenge;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Challenge
{
    /// <summary>
    /// 横長画面（Web の 2432×786 相当・2560×1080 相当）でも HUD が実画面の左上に付き、幅と罫線が画面の倍率だけ拡大されることを確かめる（L1）
    /// Verifies that on wide screens (the Web 2432x786 and the 2560x1080 equivalents) the HUD sticks to the viewport top-left and its width and rule scale only by the screen factor (L1)
    /// </summary>
    // 横幅は物理 2432px ÷ 1.0917。UITK は物理画素へ寄せるので、端数の論理幅だと要素の幅がずれる
    // Width is 2432 physical px / 1.0917; UITK snaps to physical pixels, so a rounded logical width drifts the element
    [TestFixture(2227.7182f, 720f, 1.0917f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class CurrentChallengeHudWideTest : ScreenTestHost
    {
        // Web の 2432×786 は論理 720 高で 2227.7182×720・倍率 786/720、2560×1080 は 1706.6666×720・倍率 1.5
        // The Web 2432x786 is 2227.7182x720 at scale 786/720 and 2560x1080 is 1706.6666x720 at scale 1.5 at a logical height of 720
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;

        public CurrentChallengeHudWideTest(float width, float height, float panelPixelsPerPoint)
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

        /// <web-test file="e2e/tests/challenge/hudLayout.spec.ts">横長画面で進行HUDを実画面左上へ置き罫線を約3分の1へ縮める</web-test>
        [Test]
        public void 横長画面で進行HUDを実画面左上へ置き罫線を約3分の1へ縮める()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            var view = new CurrentChallengeHudView();
            stack.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(view);
            view.Render(CurrentChallengeHudViewModel.Create(new[] { UiText.FromVerbatim("石を採掘する") }, false));
            simulate.FrameUpdate();

            // 実画面の左上（40px 以内）。物理 px の幅と罫線は Web と同じくトークン × 画面の倍率を物理画素へ寄せた値
            // At the viewport top-left (within 40px); physical widths of the HUD and rule equal the tokens times the screen scale snapped to device pixels as on the Web
            var left = ThemeTokenFile.ResolvePixels("--challenge-hud-left");
            var width = ThemeTokenFile.ResolvePixels("--challenge-hud-width");
            var padding = ThemeTokenFile.ResolvePixels("--hud-panel-padding");
            var ruleWidth = ThemeTokenFile.ResolvePixels("--challenge-hud-rule-width");
            var physicalHudLeft = PhysicalRound(left * _pixelsPerPoint);
            var physicalBodyLeft = PhysicalRound(physicalHudLeft + padding * _pixelsPerPoint);
            Assert.LessOrEqual(view.worldBound.x * _pixelsPerPoint, 40f);
            Assert.LessOrEqual(view.worldBound.y * _pixelsPerPoint, 40f);
            Assert.AreEqual(PhysicalRound((left + width) * _pixelsPerPoint) - physicalHudLeft, view.worldBound.width * _pixelsPerPoint, 0.1f);
            Assert.AreEqual(PhysicalRound(physicalBodyLeft + ruleWidth * _pixelsPerPoint) - physicalBodyLeft, view.Q(className: "hud-section-heading__rule").worldBound.width * _pixelsPerPoint, 0.1f);
            Assert.Less(ruleWidth / width, 0.34f);
        }

        // UITK は子の辺を「寄せ済みの親の辺 + 論理の差 × 倍率」から物理画素へ半分切り上げで寄せる（Mathf.Round は偶数丸めなので使わない）
        // UITK snaps a child's edges half-up to device pixels from the parent's snapped edge plus the logical offset times the scale (not Mathf.Round, which rounds half to even)
        private static float PhysicalRound(float physical)
        {
            return Mathf.Floor(physical + 0.5f);
        }
    }
}
