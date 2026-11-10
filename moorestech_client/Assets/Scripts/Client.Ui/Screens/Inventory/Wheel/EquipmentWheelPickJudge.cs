using Client.Ui.Foundation.Input;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// ポインタの下の要素がホイールを奪う UI か（Web isPointerOverWebUi && !isWheelPassthrough）
    /// Whether the element under the pointer is UI that takes the wheel (Web isPointerOverWebUi && !isWheelPassthrough)
    /// </summary>
    public static class EquipmentWheelPickJudge
    {
        public static bool IsBlockingUi(VisualElement picked)
        {
            // 何も拾わなければ世界の上。拾っても素通しの印の下ならゲームの操作（Web isPointerOverWebUi && !isWheelPassthrough）
            // Nothing picked means over the world; a pick under a passthrough mark is still the game's (Web isPointerOverWebUi && !isWheelPassthrough)
            return picked != null && !WheelPassthroughMark.IsPassthrough(picked);
        }
    }
}
