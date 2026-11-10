using Client.Ui.Foundation.Input;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Inventory.Wheel
{
    /// <summary>
    /// ポインタ下がホイールを奪う UI か
    /// Whether the element under the pointer takes the wheel
    /// </summary>
    public static class EquipmentWheelPickJudge
    {
        public static bool IsBlockingUi(VisualElement picked)
        {
            // 何も拾わない／素通しの下はゲーム操作
            // Nothing picked or under a passthrough mark is the game's
            return picked != null && !WheelPassthroughMark.IsPassthrough(picked);
        }
    }
}
