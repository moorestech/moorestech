using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Root;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    public sealed class EquipmentWheelPointerPicker : IEquipmentWheelPointerPicker
    {
        private readonly GameUiRoot _root;

        public EquipmentWheelPointerPicker(GameUiRoot root)
        {
            _root = root;
        }

        public VisualElement PickAt(Vector2 screenPosition)
        {
            // 拾いと座標換算は Foundation の probe の 1 か所に任せる（ルートのパネル接続は入力の導入が起動で確かめる）
            // Picking and conversion live in the Foundation probe (the input installer checks the root's panel at startup)
            return new GameUiPointerProbe(_root.GetRootElement().panel).PickAt(screenPosition);
        }
    }
}
