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

        protected override void SetLeadingSpace(VisualElement item, StyleLength space)
        {
            item.style.marginTop = space;
        }
    }
}
