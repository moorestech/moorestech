using Client.Ui.Foundation.Input.Pointer;
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
            // 画面座標（左下原点）→ パネル座標。換算は A-133 の唯一の式を使う
            // Screen (bottom-left origin) to panel coordinates via the single A-133 formula
            var panel = _root.GetRootElement().panel;
            return panel.Pick(UiScreenToPanel.FromBottomLeft(panel, screenPosition));
        }
    }
}
