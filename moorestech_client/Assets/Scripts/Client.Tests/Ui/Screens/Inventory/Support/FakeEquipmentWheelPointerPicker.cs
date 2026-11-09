using Client.Ui.Screens.Inventory.Wheel;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    public sealed class FakeEquipmentWheelPointerPicker : IEquipmentWheelPointerPicker
    {
        // 世界の上（何も拾わない）
        // Over the world (nothing picked)
        public VisualElement PickAt(Vector2 screenPosition)
        {
            return null;
        }
    }
}
