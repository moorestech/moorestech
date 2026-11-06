using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 横に並べ、子の間に gap を空ける（Mantine Group。縦は中央・折り返しあり）
    /// Lays children out in a row with a gap between them (Mantine Group, centered vertically, wrapping)
    /// </summary>
    /// <remarks>
    /// 折り返した 2 行目の行間は付かない（Mantine は行にも gap を付ける）。行間が要る置き方は UiStack で包む
    /// Wrapped rows get no row gap (Mantine also gaps rows); wrap in a UiStack where row spacing matters
    /// </remarks>
    public sealed class UiGroup : UiGapContainer
    {
        public UiGroup(UiGap gap) : base("ui-group", gap)
        {
        }

        protected override void SetLeadingSpace(VisualElement item, StyleLength space)
        {
            item.style.marginLeft = space;
        }
    }
}
