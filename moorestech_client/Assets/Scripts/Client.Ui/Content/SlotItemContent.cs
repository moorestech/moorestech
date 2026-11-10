using Client.Ui.Components.Slots;
using Client.Ui.State.Items;

namespace Client.Ui.Content
{
    /// <summary>
    /// SlotItem を ItemSlot の中身へ写す唯一の口
    /// The single mapping from SlotItem to ItemSlot content
    /// </summary>
    public static class SlotItemContent
    {
        public static ItemSlotContent From(GameContentPresentation content, SlotItem item, bool selected)
        {
            // 空きマスは絵も個数も持たない
            // Empty cells carry no icon or count
            var icon = item.IsEmpty ? null : content.Item(item.ItemId);
            int? count = item.IsEmpty ? null : item.Count;
            return new ItemSlotContent(icon, count, new ItemSlotMarks(selected, false, false), null, null, null);
        }
    }
}
