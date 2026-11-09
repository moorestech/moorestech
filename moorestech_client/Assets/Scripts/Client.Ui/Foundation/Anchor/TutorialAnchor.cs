using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Anchor
{
    // チュートリアルのアンカーを要素に印付けする。状態は持たず、クラス名として木に載せるだけ
    // Marks an element as a tutorial anchor; stateless, it only puts class names on the tree
    public static class TutorialAnchor
    {
        public const string MarkerClassName = "tutorial-anchor";
        public const string ClipContainerClassName = "tutorial-anchor-clip";

        public static void Mark(VisualElement element, params string[] ids)
        {
            element.AddToClassList(MarkerClassName);
            foreach (var id in ids) element.AddToClassList(id);
        }

        // USS の overflow: hidden で子を切る要素に付ける。C# は USS 由来の overflow を読めないので、祖先クリップの判定はこの印で行う（ADR 0024）
        // Put on elements whose USS overflow: hidden clips children; C# cannot read USS overflow, so the ancestor clip walk uses this mark (ADR 0024)
        public static void MarkClipContainer(VisualElement element)
        {
            element.AddToClassList(ClipContainerClassName);
        }
    }
}
