using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 横に並べ、子の間に gap を空ける（Mantine Group。縦は中央・折り返しあり）
    /// Lays children out in a row with a gap between them (Mantine Group, centered vertically, wrapping)
    /// </summary>
    /// <remarks>
    /// 折り返しても列間にも行間にも gap が付き、行頭・行末・1 行目の上には付かない（Mantine の CSS gap と同じ）
    /// Wrapped rows get the gap between columns and rows, never at row starts, ends or above the first row (same as Mantine CSS gap)
    /// 表示中の全子へ左に gap・上下に gap/2 を当て、並び要素の負の margin で相殺する。ルート自身には外側 margin を書かない
    /// Every shown child gets gap on its left and gap/2 above and below, cancelled by negative margins on the row element; the root gets no outer margin
    /// </remarks>
    public sealed class UiGroup : UiGapContainer
    {
        private readonly VisualElement _row = new();

        public UiGroup(UiGap gap) : base("ui-group", gap)
        {
            _row.AddToClassList("ui-group__row");
            Add(_row);
        }

        protected override VisualElement ItemParent => _row;

        protected override void OnGapPixelsResolved(float pixels)
        {
            _row.style.marginLeft = -pixels;
            _row.style.marginTop = -pixels / 2f;
            _row.style.marginBottom = -pixels / 2f;
        }

        protected override void ApplyItemSpace(VisualElement item, bool isFirstShown, float gapPixels)
        {
            // 縦は上下に半分ずつ。align-items: center が上だけの margin を無視して子がずれるため
            // Vertical gap is split top/bottom because align-items: center ignores a one-sided margin and shifts the child
            item.style.marginLeft = gapPixels;
            item.style.marginTop = gapPixels / 2f;
            item.style.marginBottom = gapPixels / 2f;
        }
    }
}
