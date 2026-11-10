using Client.Input;
using Client.Input.Pointer;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// ホイールを購読し px と UI 上かを添えてルーターへ
    /// Subscribes to the wheel and hands px and over-UI to the router
    /// </summary>
    public sealed class EquipmentWheelInput : IInitializable
    {
        private readonly InputSystemPointerWheelStream _wheel;
        private readonly InputSystemPointerPositionStream _positions;
        private readonly IEquipmentWheelPointerPicker _picker;
        private readonly EquipmentWheelRouter _router;
        private Vector2 _pointerOnScreen;

        public EquipmentWheelInput(InputSystemPointerWheelStream wheel, InputSystemPointerPositionStream positions, IEquipmentWheelPointerPicker picker, EquipmentWheelRouter router)
        {
            _wheel = wheel;
            _positions = positions;
            _picker = picker;
            _router = router;
        }

        public void Initialize()
        {
            // 位置は変わった時だけ届くので、起動時の値を一度読む（前例 InputSystemUiScreenPointer.CurrentPointerOnScreen）
            // Positions arrive only on change, so read the startup value once (precedent InputSystemUiScreenPointer.CurrentPointerOnScreen)
            _pointerOnScreen = (Vector2)HybridInput.GetMousePosition();
            _positions.OnPointerScreenPositionChanged.Subscribe(position => _pointerOnScreen = position);
            _wheel.OnWheelNotchesScrolled.Subscribe(OnWheel);

            #region Internal

            void OnWheel(float notchesUp)
            {
                // ロック中はカーソルが画面中央に固定され HUD の上に来ない。ロックの正本は InputManager（A-134）
                // While locked the cursor stays centred and never over a HUD; InputManager owns the lock state (A-134)
                var overBlockingUi = !InputManager.IsCursorLocked && EquipmentWheelPickJudge.IsBlockingUi(_picker.PickAt(_pointerOnScreen));
                _router.Route(EquipmentWheelNotchScale.ToDownwardPixels(notchesUp), WheelDeltaUnit.Pixel, overBlockingUi);
            }

            #endregion
        }
    }
}
