using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// 実 UI のままホイールだけゲームへ素通す印（Web の wheel-passthrough の写し。S3-hotbar-modal〈A-109〉もこれを使う）
    /// Mark that keeps an element real UI but lets only the wheel pass to the game (copy of Web wheel-passthrough; S3-hotbar-modal (A-109) uses it too)
    /// </summary>
    /// <remarks>
    /// 判定は祖先をたどる（Web の closest）ので、印は部分木の根に 1 つ付ければ足りる
    /// The check walks ancestors (Web closest), so one mark on the subtree root is enough
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
