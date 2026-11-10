using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.CommonHud;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Inventory.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory
{
    public class EquipmentWheelFlowTest : ScreenTestHost
    {
        private static readonly Vector2 OverWorld = new(5, 5);
        private GameObject _host;
        private VisualElement _viewport;
        private FakeEquipmentSelectionPort _port;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("equipment-wheel-flow");
            _viewport = UiLayerStack.Build(rootVisualElement).GetLayer(UiLayer.Chrome, UiFamily.Viewport);
            _port = new FakeEquipmentSelectionPort(3);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">小さいホイール入力を累積し閾値を越えた時だけ切り替える</web-test>
        [Test]
        public void 小さいホイールは累積して閾値を越えたときだけ切り替える()
        {
            var wheel = Wheel(UIStateEnum.GameScreen);
            WheelAt(wheel, OverWorld, 40);
            Assert.IsEmpty(_port.Selections);
            WheelAt(wheel, OverWorld, 70);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        [Test]
        public void 下へ1ノッチ目では進まず2ノッチ目で1段進む()
        {
            // Web の本番（CEF の 60px/ノッチ・100px で 1 段・段が出たら端数を捨てる）と同じ刻み（D-10）
            // Same cadence as the production Web (CEF 60px per notch, 100px per step, remainder dropped on a step; D-10)
            var wheel = Wheel(UIStateEnum.GameScreen);
            var oneNotchDown = EquipmentWheelNotchScale.ToDownwardPixels(-1f);
            WheelAt(wheel, OverWorld, oneNotchDown);
            Assert.IsEmpty(_port.Selections);
            WheelAt(wheel, OverWorld, oneNotchDown);
            WheelAt(wheel, OverWorld, oneNotchDown);
            WheelAt(wheel, OverWorld, oneNotchDown);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">ホイールは末尾スロットの次に先頭スロットへ回る</web-test>
        [Test]
        public void 末尾の次は先頭へ回る()
        {
            var wheel = Wheel(UIStateEnum.GameScreen);
            for (var step = 1; step < 3; step++)
            {
                WheelAt(wheel, OverWorld, 100);
                _port.ConfirmFromServer(step);
            }
            WheelAt(wheel, OverWorld, 100);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">画面中央のクロスヘアの上でもホイールで装備が切り替わる</web-test>
        [Test]
        public void クロスヘアの上でも切り替わる()
        {
            var crosshair = new CrosshairHudView();
            crosshair.SetShown(true);
            _viewport.Add(crosshair);
            simulate.FrameUpdate();

            WheelAt(Wheel(UIStateEnum.GameScreen), crosshair.Q("crosshair").worldBound.center, 100);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/inventory/equipment.spec.ts">持ち物画面の一覧スクロールでは装備が持ち替わらない</web-test>
        [Test]
        public void 持ち物画面の一覧の上のホイールでは持ち替えず外では持ち替える()
        {
            // 一覧（S4-recipe の ItemListPanel）の代わりに、素通しの印の無いスクロール器を立てる（D-6）
            // Stand in for the list (S4-recipe's ItemListPanel) with a scroll view that has no passthrough mark (D-6)
            var list = new GameScrollView("item-list-grid");
            list.style.position = Position.Absolute;
            list.style.left = 100;
            list.style.top = 100;
            list.style.width = 200;
            list.style.height = 200;
            _viewport.Add(list);
            simulate.FrameUpdate();

            var wheel = Wheel(UIStateEnum.PlayerInventory);
            for (var step = 0; step < 3; step++) WheelAt(wheel, list.worldBound.center, 100);
            WheelAt(wheel, new Vector2(1, 1), 100);

            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        /// <web-test file="e2e/tests/regression/inputLayer.spec.ts">GameScreenのホイールは最新equipment値から次スロットを選ぶ</web-test>
        [Test]
        public void ゲーム画面のホイールは最新の装備の値から次を選ぶ()
        {
            // 選択 2 の次は 0
            // After selection 2 comes 0
            var wheel = Wheel(UIStateEnum.GameScreen);
            _port.ConfirmFromServer(2);
            WheelAt(wheel, OverWorld, 100);
            CollectionAssert.AreEqual(new[] { 0 }, _port.Selections);
        }

        [Test]
        public void モーダルとツールの占有と前面の画面はルーターが本物のStateから読んで止める()
        {
            // ルーターが状況を組む経路（画面・モーダル・占有の読み取り）を本物の State で通す（Gate の規則は L0 が見る）
            // Drive the router's own assembly (screen, modal, ownership reads) through real State (the gate rules are L0's)
            var modal = Wheel(UIStateEnum.GameScreen);
            modal.Modal.RequestInputModal("title", "message", "ok");
            WheelAt(modal, OverWorld, 100);
            var tool = Wheel(UIStateEnum.GameScreen);
            tool.Placement.SetWheelOwnedByTool(true);
            WheelAt(tool, OverWorld, 100);
            WheelAt(Wheel(UIStateEnum.SubInventory), OverWorld, 100);

            Assert.IsEmpty(_port.Selections);
        }

        private EquipmentWheelRouterFixture Wheel(UIStateEnum state)
        {
            return new EquipmentWheelRouterFixture(_host, state, _port);
        }

        private void WheelAt(EquipmentWheelRouterFixture wheel, Vector2 panelPosition, double pixelsDown)
        {
            var picked = rootVisualElement.panel.Pick(panelPosition);
            wheel.Router.Route(pixelsDown, WheelDeltaUnit.Pixel, EquipmentWheelPickJudge.IsBlockingUi(picked));
        }
    }
}
