using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// ホイールだけゲームへ素通す印（A-109 も使う）
    /// Mark that lets only the wheel pass to the game (A-109 uses it too)
    /// </summary>
    /// <remarks>
    /// 祖先をたどるので根に 1 つで足りる
    /// Ancestors are walked, so one mark on the root suffices
    /// </remarks>
    public static class WheelPassthroughMark
    {
        public const string ClassName = "wheel-passthrough";

        public static void Mark(VisualElement element)
        {
            element.AddToClassList(ClassName);
        }

        public static bool IsPassthrough(VisualElement target)
        {
            // 自身か祖先に印があれば素通し。何も拾っていなければ偽
            // Passthrough if itself or an ancestor carries the mark; false when nothing was picked
            for (var element = target; element != null; element = element.parent)
            {
                if (element.ClassListContains(ClassName)) return true;
            }
            return false;
        }
    }
}
