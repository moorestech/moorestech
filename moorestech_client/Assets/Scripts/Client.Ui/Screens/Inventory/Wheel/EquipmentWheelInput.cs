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
        private readonly IEquipmentWheelPointerPicker _picker;
        private readonly EquipmentWheelRouter _router;

        public EquipmentWheelInput(InputSystemPointerWheelStream wheel, IEquipmentWheelPointerPicker picker, EquipmentWheelRouter router)
        {
            _wheel = wheel;
            _picker = picker;
            _router = router;
        }

        public void Initialize()
        {
            _wheel.OnWheelNotchesScrolled.Subscribe(wheel => OnWheel(wheel.notchesUp, wheel.screenPosition));

            #region Internal

            void OnWheel(float notchesUp, Vector2 screenPosition)
            {
                // ロック中はカーソルが画面中央に固定され HUD の上に来ない。ロックの正本は InputManager（A-134）
                // While locked the cursor stays centred and never over a HUD; InputManager owns the lock state (A-134)
                // 位置はホイールと同じイベントが運ぶ。デバイスの今の値はこのイベントの移動を含まず縁で 1 つ前の位置になる（A-133）
                // The wheel's own event carries the position; the device value lacks this event's move and lags at the edge (A-133)
                var overBlockingUi = !InputManager.IsCursorLocked && EquipmentWheelPickJudge.IsBlockingUi(_picker.PickAt(screenPosition));
                _router.Route(EquipmentWheelNotchScale.ToDownwardPixels(notchesUp), WheelDeltaUnit.Pixel, overBlockingUi);
            }

            #endregion
        }
    }
}
