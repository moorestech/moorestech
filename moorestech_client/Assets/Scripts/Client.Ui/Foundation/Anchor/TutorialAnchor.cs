using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
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

        // id を外し、残らなければ印のクラスも外す
        // Removes ids and drops the marker class when none remain
        public static void Unmark(VisualElement element, params string[] ids)
        {
            foreach (var id in ids) element.RemoveFromClassList(id);
            if (element.GetClasses().Any(IsAnchorId)) return;
            element.RemoveFromClassList(MarkerClassName);

            #region Internal

            // クラス名がアンカー id か（固定の id か、マスタが直書きする動的な接頭辞の id）
            // Whether a class name is an anchor id (a static id or one with a master-written dynamic prefix)
            bool IsAnchorId(string className)
            {
                if (AnchorIds.StaticIds.Contains(className)) return true;
                return AnchorIds.DynamicPrefixes.Any(className.StartsWith);
            }

            #endregion
        }

        // USS の overflow: hidden で子を切る要素に付ける。C# は USS 由来の overflow を読めないので、祖先クリップの判定はこの印で行う（ADR 0024）
        // Put on elements whose USS overflow: hidden clips children; C# cannot read USS overflow, so the ancestor clip walk uses this mark (ADR 0024)
        public static void MarkClipContainer(VisualElement element)
        {
            element.AddToClassList(ClipContainerClassName);
        }
    }
}
