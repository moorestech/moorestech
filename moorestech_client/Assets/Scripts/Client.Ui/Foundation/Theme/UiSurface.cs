using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// 面（パネル・紙）を張る部品の印。中の要素が世界の地ではなく面の上にあるかを引く
    /// Marks components that lay a surface (panel or paper); tells whether an element sits on a surface rather than the world
    /// </summary>
    /// <remarks>
    /// 半透明の色の Linear 合成は地の明るさで Web との差が変わるので、地に合わせて色を選ぶ部品（チュートリアル枠のグロー）がこれを読む。
    /// Linear blending of a translucent color drifts from the Web depending on ground brightness, so parts that pick a color per ground (the tutorial glow) read this.
    /// </remarks>
    public static class UiSurface
    {
        private const string SurfaceClassName = "ui-surface";

        public static void Mark(VisualElement surface)
        {
            surface.AddToClassList(SurfaceClassName);
        }

        // 要素自身は数えず祖先だけを見る（要素の外側に描くものは、要素が面でもその外の地に載る）
        // Only ancestors count, not the element itself (whatever draws outside the element lands on the ground beyond it)
        public static bool HasSurfaceAncestor(VisualElement element)
        {
            for (var node = element.hierarchy.parent; node != null; node = node.hierarchy.parent)
            {
                if (node.ClassListContains(SurfaceClassName)) return true;
            }
            return false;
        }
    }
}
