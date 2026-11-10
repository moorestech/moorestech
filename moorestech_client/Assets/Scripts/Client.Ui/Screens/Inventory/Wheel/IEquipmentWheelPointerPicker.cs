using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    public interface IEquipmentWheelPointerPicker
    {
        // 画面座標（左下原点）の下にある要素。何も無ければ null
        // The element under a screen position (bottom-left origin); null when nothing is there
        VisualElement PickAt(Vector2 screenPosition);
    }
}
