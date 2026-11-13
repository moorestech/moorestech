using Client.Input;
using Client.Input.Pointer;
using Client.Ui.Foundation.Input.Pointer;
using Client.Ui.State.Inventory.Equipment;
using Client.Ui.State.Inventory.Logic;
using UniRx;
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
        private readonly IUiScreenPointer _pointer;
        private readonly IEquipmentWheelPointerPicker _picker;
        private readonly EquipmentWheelRouter _router;

        public EquipmentWheelInput(InputSystemPointerWheelStream wheel, IUiScreenPointer pointer, IEquipmentWheelPointerPicker picker, EquipmentWheelRouter router)
        {
            _wheel = wheel;
            _pointer = pointer;
            _picker = picker;
            _router = router;
        }

        public void Initialize()
        {
            _wheel.OnWheelNotchesScrolled.Subscribe(OnWheel);

            #region Internal

            void OnWheel(float notchesUp)
            {
                // ロック中はカーソルが画面中央に固定され HUD の上に来ない。ロックの正本は InputManager（A-134）
                // While locked the cursor stays centred and never over a HUD; InputManager owns the lock state (A-134)
                // ポインタは掴み絵・ツールチップと同じ Foundation の口からホイールの時点で読む（A-133）
                // Read the pointer at wheel time from the same Foundation port as the grab picture and tooltip (A-133)
                var overBlockingUi = !InputManager.IsCursorLocked && EquipmentWheelPickJudge.IsBlockingUi(_picker.PickAt(_pointer.CurrentPointerOnScreen()));
                _router.Route(EquipmentWheelNotchScale.ToDownwardPixels(notchesUp), WheelDeltaUnit.Pixel, overBlockingUi);
            }

            #endregion
        }
    }
}
