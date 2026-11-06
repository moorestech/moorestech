using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// GameUi の Pick の probe が、層コンテナを除いた拾える要素の上でだけ true を返すことを実パネルで確かめる（L1）
    /// Verifies on a real panel that the GameUi Pick probe is true only over pickable content, never over layer containers (L1)
    /// </summary>
    /// <remarks>
    /// スクリーン座標→パネル座標の換算はテスト用パネルでは RuntimePanelUtils が使えないので、ここではパネル座標で試し、換算は L3 で試す
    /// The test panel cannot use RuntimePanelUtils for the screen-to-panel conversion, so this tests panel coordinates and leaves the conversion to L3
    /// </remarks>
    public class GameUiPointerProbeTest : UiTestHost
    {
        private static readonly Vector2 PanelCenter = new(640, 360);

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiPointerProbe(null);
        }

        [Test]
        public void EmptyLayersAreNotOverGameUi()
        {
            UiLayerStack.Build(rootVisualElement);
            simulate.FrameUpdate();

            Assert.IsFalse(new GameUiPointerProbe(panel).IsPickableAt(PanelCenter));
        }

        [Test]
        public void PositionPickedBoxIsOverGameUi()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            stack.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(CreateCenteredBox(PickingMode.Position));
            simulate.FrameUpdate();

            // 箱の上は true、箱の外は層だけなので false
            // True over the box, false outside it where only layers remain
            var probe = new GameUiPointerProbe(panel);
            Assert.IsTrue(probe.IsPickableAt(PanelCenter));
            Assert.IsFalse(probe.IsPickableAt(new Vector2(10, 10)));
        }

        [Test]
        public void IgnorePickedBoxIsNotOverGameUi()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            stack.GetLayer(UiLayer.Modal, UiFamily.Viewport).Add(CreateCenteredBox(PickingMode.Ignore));
            simulate.FrameUpdate();

            Assert.IsFalse(new GameUiPointerProbe(panel).IsPickableAt(PanelCenter));
        }

        private static VisualElement CreateCenteredBox(PickingMode pickingMode)
        {
            var box = new VisualElement { name = "probe-box", pickingMode = pickingMode };
            box.style.position = Position.Absolute;
            box.style.left = PanelCenter.x - 100;
            box.style.top = PanelCenter.y - 50;
            box.style.width = 200;
            box.style.height = 100;
            return box;
        }
    }
}
