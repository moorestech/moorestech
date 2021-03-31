using industrialization.Item;

namespace industrialization.Inventory
{
    public class InventoryData
    {
        public ItemStack[] ItemStacks;

        public InventoryData(int slot)
        {
            ItemStacks = new ItemStack[slot];
        }
    }
}