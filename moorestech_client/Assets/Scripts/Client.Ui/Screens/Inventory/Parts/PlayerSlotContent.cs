using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.Inventory.Parts
{
    /// <summary>
    /// 枠の中身を ItemSlot の中身へ写す
    /// Maps slot item to ItemSlot content
    /// </summary>
    public static class PlayerSlotContent
    {
        public static ItemSlotContent From(GameContentPresentation content, SlotItem item, bool selected)
        {
            var icon = item.IsEmpty ? null : content.Item(item.ItemId);
            return new ItemSlotContent(icon, item.IsEmpty ? null : item.Count, new ItemSlotMarks(selected, false, false), null, null, null);
        }
    }
}
