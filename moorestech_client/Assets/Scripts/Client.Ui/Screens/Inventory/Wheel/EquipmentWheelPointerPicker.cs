using Client.Ui.Foundation.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    public sealed class EquipmentWheelPointerPicker : IEquipmentWheelPointerPicker
    {
        private readonly GameUiPointerProbe _probe;

        public EquipmentWheelPointerPicker(GameUiPointerProbe probe)
        {
            _probe = probe;
        }

        public VisualElement PickAt(Vector2 screenPosition)
        {
            // 拾いと座標換算は probe に任せる
            // Picking and conversion live in the DI probe
            return _probe.PickAt(screenPosition);
        }
    }
}
