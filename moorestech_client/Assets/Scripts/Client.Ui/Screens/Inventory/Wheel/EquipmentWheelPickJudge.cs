using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// ポインタの下の要素がホイールを奪う UI か（Web isPointerOverWebUi。素通しの印は Task 9 で足す）
    /// Whether the element under the pointer is UI that takes the wheel (Web isPointerOverWebUi; the passthrough mark comes in Task 9)
    /// </summary>
    public static class EquipmentWheelPickJudge
    {
        public static bool IsBlockingUi(VisualElement picked)
        {
            // 何も拾わなければ世界の上（拾わない HUD・クロスヘアを含む）
            // Nothing picked means over the world (including non-picking HUDs and the crosshair)
            return picked != null;
        }
    }
}
