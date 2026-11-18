using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Rendering;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.CommonHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CommonHud
{
    /// <summary>
    /// クロスヘアが表示の間だけ舞台の中央に 6×6 の白い円で出て、影が Web の box-shadow 0 0 2px と同じ減衰で描かれることを確かめる（L1）
    /// Verifies the crosshair shows as a 6x6 white circle at the stage center only while visible, with a shadow falling off like the Web box-shadow 0 0 2px (L1)
    /// </summary>
    public class CrosshairHudViewTest : UiTestHost
    {
        private VisualElement _stageLayer;
        private CrosshairHudView _view;

        [SetUp]
        public void MountView()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            _stageLayer = stack.GetLayer(UiLayer.ViewportStage, UiFamily.Stage);
            _view = new CrosshairHudView();
            _stageLayer.Add(_view);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">採掘進捗・クロスヘア・tooltipのtopic eventを表示する</web-test>
        [Test]
        public void クロスヘアは表示の間だけ舞台の中央に白い円で出る()
        {
            var crosshair = _view.Q("crosshair");
            _view.SetShown(true);
            simulate.FrameUpdate();

            // 舞台の中央に 6×6。点は白の円で、チュートリアルのアンカーを持つ
            // 6x6 at the stage center; the dot is a white circle carrying the tutorial anchor
            Assert.AreEqual(DisplayStyle.Flex, crosshair.resolvedStyle.display);
            Assert.AreEqual(_stageLayer.worldBound.center.x, crosshair.worldBound.center.x, 0.05f);
            Assert.AreEqual(_stageLayer.worldBound.center.y, crosshair.worldBound.center.y, 0.05f);
            Assert.AreEqual(6f, crosshair.worldBound.width, 0.05f);
            Assert.AreEqual(6f, crosshair.worldBound.height, 0.05f);
            var dot = crosshair.Q(className: "crosshair-hud__dot");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-white"), dot.resolvedStyle.backgroundColor, "dot");
            Assert.AreEqual(3f, dot.resolvedStyle.borderTopLeftRadius, 0.05f);
            Assert.IsTrue(crosshair.ClassListContains(TutorialAnchor.MarkerClassName));
            Assert.IsTrue(crosshair.ClassListContains(AnchorIds.GameCrosshair));

            // 隠すと消える
            // Hiding removes it
            _view.SetShown(false);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, crosshair.resolvedStyle.display);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 影はWebのbox_shadow_0_0_2pxと同じ減衰で円の外へ広がる()
        {
            // Web の blur 2px は σ=1px のガウスの畳み込み。半平面の被覆率は縁で 0.5、σ で 0.1587、3σ で 0.0013
            // The Web blur 2px is a Gaussian with sigma 1px; half-plane coverage is 0.5 at the edge, 0.1587 at sigma, 0.0013 at 3 sigma
            Assert.AreEqual(0.5f, GaussianEdgeCoverage.At(0f, 1f), 1e-4f);
            Assert.AreEqual(0.3085f, GaussianEdgeCoverage.At(0.5f, 1f), 1e-4f);
            Assert.AreEqual(0.1587f, GaussianEdgeCoverage.At(1f, 1f), 1e-4f);
            Assert.AreEqual(0.0228f, GaussianEdgeCoverage.At(2f, 1f), 1e-4f);
            Assert.AreEqual(0.0013f, GaussianEdgeCoverage.At(3f, 1f), 1e-4f);
            Assert.AreEqual(0f, GaussianEdgeCoverage.At(5f, 1f), 1e-4f);
        }
    }
}
