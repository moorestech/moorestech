using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 縦に並べ、子の間に gap を空ける（Mantine Stack。揃えは stretch）
    /// Stacks children vertically with a gap between them (Mantine Stack, align stretch)
    /// </summary>
    public sealed class UiStack : UiGapContainer
    {
        public UiStack(UiGap gap) : base("ui-stack", gap)
        {
        }

        protected override void ApplyItemSpace(VisualElement item, bool isFirstShown, float gapPixels)
        {
            // 表示中の先頭は USS の余白に任せ、それ以降の上に gap を空ける
            // The first shown child keeps its USS margin; later ones get the gap above
            item.style.marginTop = isFirstShown ? new StyleLength(StyleKeyword.Null) : new StyleLength(gapPixels);
        }
    }
}
