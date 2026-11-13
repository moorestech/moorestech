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
            new EquipmentWheelInput(new InputSystemPointerWheelStream(), picker, router).Initialize();
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

        [Test]
        public void 縁をまたぐ移動とホイールが1つのイベントに載ってもそのイベントの位置で判定する()
        {
            var list = AddBox(false);
            var outside = new Vector2(100f, 100f);
            var inside = list.worldBound.center;

            // 外から一覧の上へ入る移動とホイールを 1 つの MouseState に積む。デバイスの今の値で判定すると外と読んで選んでしまう
            // Stack the move from outside onto the list and the wheel in one MouseState; judging by the device value reads outside and selects
            for (var i = 0; i < 2; i++)
            {
                QueueAndUpdate(new MouseState { position = outside });
                QueueAndUpdate(new MouseState { position = inside, scroll = new Vector2(0f, -1f) });
            }
            Assert.IsEmpty(_port.Selections, "一覧の上へ入った瞬間のホイールで装備を選んだ");

            // 逆向き: 一覧の上から外へ出る移動とホイールを 1 つに積むと、外として選ぶ（何も流れていないのに緑になるのを防ぐ）
            // Reverse: the move off the list plus the wheel in one event selects as outside (guards against a vacuous green)
            for (var i = 0; i < 2; i++)
            {
                QueueAndUpdate(new MouseState { position = inside });
                QueueAndUpdate(new MouseState { position = outside, scroll = new Vector2(0f, -1f) });
            }
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        private void QueueAndUpdate(MouseState state)
        {
            // 1 イベントずつ処理する。同じ更新に並べると Mouse がイベントを併合して前の位置が消える
            // Process one event per update; queued together, the Mouse merges events and drops the earlier position
            InputSystem.QueueStateEvent(_mouse, state);
            InputSystem.Update();
        }

        private VisualElement AddBox(bool passthrough)
        {
            var box = new VisualElement { pickingMode = PickingMode.Position };
            // 箱を原点から離す。初期ポインタ(0,0)が箱の中に入ると、ホイールの時点の位置を読み落としても緑になるため
            // Keep the box away from the origin; a box under the initial pointer (0,0) would stay green even if the wheel-time position read were dropped
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
