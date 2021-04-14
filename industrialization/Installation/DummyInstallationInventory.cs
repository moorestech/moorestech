using System.Collections.Generic;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation
{
    public class DummyInstallationInventory : IInstallationInventory
    {
        public List<IItemStack> insertedItems = new List<IItemStack>();
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < insertedItems.Count; i++)
            {
                if (insertedItems[i].Id != itemStack.Id) continue;
                var r = insertedItems[i].AddItem(itemStack);
                insertedItems[i] = r.MineItemStack;
                return r.ReceiveItemStack;
            }
            insertedItems.Sort((i,j) => i.Id - j.Id);
            return new NullItemStack();
        }
    }
}