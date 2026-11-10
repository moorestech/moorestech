using Client.Ui.Components.Slots;
using Client.Ui.State.Items;

namespace Client.Ui.Content
{
    /// <summary>
    /// 枠の中身（SlotItem）を ItemSlot の中身へ写す唯一の口。持ち物・装備・ブロックの枠が共有する
    /// The single mapping from a slot item to ItemSlot content, shared by inventory, equipment and block slots
    /// </summary>
    public static class SlotItemContent
    {
        public static ItemSlotContent From(GameContentPresentation content, SlotItem item, bool selected)
        {
            // 空きマスは絵も個数も持たない（ItemSlot が空きの面で描く）
            // Empty cells carry no icon or count (ItemSlot draws the empty face)
            var icon = item.IsEmpty ? null : content.Item(item.ItemId);
            int? count = item.IsEmpty ? null : item.Count;
            return new ItemSlotContent(icon, count, new ItemSlotMarks(selected, false, false), null, null, null);
        }
    }
}
