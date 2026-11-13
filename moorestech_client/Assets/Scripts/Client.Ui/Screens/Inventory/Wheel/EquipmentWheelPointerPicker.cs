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
            // 拾いと座標換算は DI の 1 つの probe に任せる（ルートのパネル接続は入力の導入が起動で確かめる）
            // Picking and conversion live in the single DI probe (the input installer checks the root's panel at startup)
            return _probe.PickAt(screenPosition);
        }
    }
}
