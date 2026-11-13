using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Input.Pointer;
using Client.Tests.Input.Pointer;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.Inventory.Equipment;
using Client.Ui.Screens.Inventory.Wheel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Tests.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// Editor 更新では選ばず 2 ノッチで 1 段進む
    /// No selection on Editor updates; one step per two notches
    /// </summary>
    public class EquipmentWheelInputFlowTest : ScreenTestHost
    {
        private InputTestFixture _input;
        private GameObject _host;
        private FakeEquipmentSelectionPort _port;
        private Mouse _mouse;
        private List<InputUpdateType> _rawWheelUpdates;

        [SetUp]
        public void SetUp()
        {
            _input = new InputTestFixture();
            _input.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _host = new GameObject("equipment-wheel-input-flow");
            _port = new FakeEquipmentSelectionPort(3);
            var router = new EquipmentWheelRouterFixture(_host, UIStateEnum.GameScreen, _port).Router;
            new EquipmentWheelInput(new InputSystemPointerWheelStream(), new FakeEquipmentWheelPointerPicker(), router).Initialize();
            _rawWheelUpdates = new List<InputUpdateType>();
            InputSystem.onEvent += RecordRawWheelUpdate;
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.onEvent -= RecordRawWheelUpdate;
            Object.DestroyImmediate(_host);
            _input.TearDown();
        }

        [Test]
        public void Editorの更新で届いたホイールでは装備を選ばない()
        {
            // Inspector などにフォーカスがあるとき、ポインタのイベントは Editor の更新で届く
            // With focus on e.g. the Inspector, pointer events arrive in the Editor update
            InputTestDriver.LoseGameFocus(_input);
            WheelDownOneNotch(InputUpdateType.Editor);
            WheelDownOneNotch(InputUpdateType.Editor);

            CollectionAssert.Contains(_rawWheelUpdates, InputUpdateType.Editor, "前提: 生の onEvent が Editor の更新で受けた");
            Assert.IsEmpty(_port.Selections);
        }

        [Test]
        public void ゲームの更新では2ノッチで1段進む()
        {
            WheelDownOneNotch(InputUpdateType.Dynamic);
            Assert.IsEmpty(_port.Selections, "1 ノッチ目（60px）では進まない");
            WheelDownOneNotch(InputUpdateType.Dynamic);
            CollectionAssert.AreEqual(new[] { 1 }, _port.Selections);
        }

        private void WheelDownOneNotch(InputUpdateType updateType)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { scroll = new Vector2(0f, -1f) });
            InputTestDriver.RunUpdate(updateType);
        }

        private void RecordRawWheelUpdate(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == _mouse) _rawWheelUpdates.Add(InputState.currentUpdateType);
        }
    }
}
