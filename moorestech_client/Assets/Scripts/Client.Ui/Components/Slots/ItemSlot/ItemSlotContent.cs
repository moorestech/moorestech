using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// ItemSlot の入力値（Web の props）
    /// Input values of an ItemSlot (the Web props)
    /// </summary>
    /// <remarks>
    /// 優先: 実物 &gt; ゴースト。白面は実物だけで決める（ItemSlotPresentation.From）。Web の props と同じ平らな形で、Fluid の sealed 派生とは揃えない（D-9）
    /// Priority: real item &gt; ghost. The white face is decided by the real item only (ItemSlotPresentation.From). Flat like the Web props, deliberately not aligned with the sealed Fluid derivations (D-9)
    /// </remarks>
    public sealed class ItemSlotContent
    {
        // null は空きマス（Web の itemId 0）
        // null is an empty cell (the Web itemId 0)
        public UiIcon Icon { get; }

        // Web の count? の 3 状態: null＝未指定（絵だけ）・0＝空・正＝所持数
        // The three states of the Web count?: null = unspecified (icon only), 0 = empty, positive = owned count
        public int? Count { get; }

        public ItemSlotMarks Marks { get; }
        public ItemSlotShortage Shortage { get; }
        public ItemSlotGhost Ghost { get; }

        // null は表示中の名前を使う（Web の tooltip 未指定）
        // null uses the shown name (the Web tooltip unset)
        public UiText TooltipOverride { get; }

        public ItemSlotContent(UiIcon icon, int? count, ItemSlotMarks marks, ItemSlotShortage shortage, ItemSlotGhost ghost, UiText tooltipOverride)
        {
            Icon = icon;
            Count = count;
            Marks = marks;
            Shortage = shortage;
            Ghost = ghost;
            TooltipOverride = tooltipOverride;
        }
    }
}
