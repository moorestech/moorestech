using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// ItemSlot に入れる値の束（Web の ItemSlot の props）
    /// The bundle of values for an ItemSlot (the Web ItemSlot props)
    /// </summary>
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

        // null は表示中のアイコンの名前を使う（Web の tooltip 未指定）
        // null uses the shown icon's name (the Web tooltip left unset)
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
