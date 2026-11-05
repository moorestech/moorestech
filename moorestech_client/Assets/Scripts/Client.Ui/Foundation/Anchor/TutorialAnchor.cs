using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Anchor
{
    // チュートリアルのアンカーを要素に印付けする。状態は持たず、クラス名として木に載せるだけ
    // Marks an element as a tutorial anchor; stateless, it only puts class names on the tree
    public static class TutorialAnchor
    {
        public const string MarkerClassName = "tutorial-anchor";

        public static void Mark(VisualElement element, params string[] ids)
        {
            element.AddToClassList(MarkerClassName);
            foreach (var id in ids) element.AddToClassList(id);
        }
    }
}
