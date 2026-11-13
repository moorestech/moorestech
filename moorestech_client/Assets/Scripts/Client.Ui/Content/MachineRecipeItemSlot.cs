using Client.Ui.Components.Slots;
using Core.Master;

namespace Client.Ui.Content
{
    /// <summary>
    /// 機械レシピの行のアイテムの枠とその ID
    /// An item slot of a machine recipe row with its ID, read by users that select the item on press
    /// </summary>
    public readonly struct MachineRecipeItemSlot
    {
        public readonly ItemSlot Slot;
        public readonly ItemId ItemId;

        public MachineRecipeItemSlot(ItemSlot slot, ItemId itemId)
        {
            Slot = slot;
            ItemId = itemId;
        }
    }
}
