using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Layout
{
    /// <summary>
    /// 横並び・gap・縦中央・折り返し
    /// Row with gap, centered, wrapping
    /// </summary>
    /// <remarks>
    /// 行頭行末に gap なし
    /// No gap at row starts or ends
    /// 負の margin で相殺
    /// Cancelled by negative margins
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
