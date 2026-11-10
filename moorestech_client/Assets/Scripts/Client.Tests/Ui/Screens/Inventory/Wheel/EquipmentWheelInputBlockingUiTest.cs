using Client.Game.InGame.UI.UIState;
using Client.Input.Pointer;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Inventory.Wheel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// UI 上のホイールで装備を切り替えない
    /// The wheel over UI does not switch equipment
    /// </summary>
    public class EquipmentWheelInputBlockingUiTest : ScreenTestHost
    {
        private InputTestFixture _input;
        private GameObject _host;
        private FakeEquipmentSelectionPort _port;
        private Mouse _mouse;

        [SetUp]
        public void SetUp()
        {
            _input = new InputTestFixture();
            _input.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _host = new GameObject("equipment-wheel-input-blocking-ui");
            _port = new FakeEquipmentSelectionPort(3);
            var router = new EquipmentWheelRouterFixture(_host, UIStateEnum.GameScreen, _port).Router;
            var picker = new PanelWheelPointerPicker(rootVisualElement);
            new EquipmentWheelInput(new InputSystemPointerWheelStream(), new InputSystemPointerPositionStream(), picker, router).Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            _input.TearDown();
        }

        [Test]
        public void 持ち物の一覧の上のホイールでは装備を選ばない()
        {
            var list = AddBox(false);
            WheelDownTwoNotchesOver(list);
            Assert.IsEmpty(_port.Selections);
        }

        [Test]
        public void 素通しの印がある装備HUDの上のホイールでは装備を選ぶ()
        {
            var hud = AddBox(true);
            WheelDownTwoNotchesOver(hud);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        private VisualElement AddBox(bool passthrough)
        {
            var box = new VisualElement { pickingMode = PickingMode.Position };
            // 箱を原点から離す。初期ポインタ(0,0)が箱の中に入ると、位置の購読を落としても緑になるため
            // Keep the box away from the origin; a box under the initial pointer (0,0) would stay green even without the position subscription
            box.style.position = Position.Absolute;
            box.style.left = 300;
            box.style.top = 300;
            box.style.width = 200;
            box.style.height = 200;
            if (passthrough) WheelPassthroughMark.Mark(box);
            rootVisualElement.Add(box);
            simulate.FrameUpdate();
            return box;
        }

        private void WheelDownTwoNotchesOver(VisualElement target)
        {
            // テストのパネルは座標そのままなので、対象の中心をポインタ位置にする
            // The test panel uses coordinates as-is, so the target centre is the pointer position
            var position = target.worldBound.center;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            InputSystem.Update();
            for (var i = 0; i < 2; i++)
            {
                InputSystem.QueueStateEvent(_mouse, new MouseState { position = position, scroll = new Vector2(0f, -1f) });
                InputSystem.Update();
            }
        }
    }
}
