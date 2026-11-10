using Client.Ui.Screens.Inventory.Wheel;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// 座標そのままで拾う picker
    /// Picker that hits with coordinates as-is
    /// 本番の換算（UiScreenToPanel）は実行時パネルと UIDocument を要するので代える。FromBottomLeft の換算は L3 録画に任せる
    /// Production conversion (UiScreenToPanel) needs a runtime panel and UIDocument, so this stands in; FromBottomLeft is left to the L3 recording
    /// </summary>
    public sealed class PanelWheelPointerPicker : IEquipmentWheelPointerPicker
    {
        private readonly VisualElement _rootElement;

        public PanelWheelPointerPicker(VisualElement rootElement)
        {
            _rootElement = rootElement;
        }

        public VisualElement PickAt(Vector2 screenPosition)
        {
            return _rootElement.panel.Pick(screenPosition);
        }
    }
}
