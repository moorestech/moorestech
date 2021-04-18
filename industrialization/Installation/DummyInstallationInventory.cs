using System.Collections.Generic;
using System.Linq;
using industrialization.Item;

namespace industrialization.Installation
{
    public class DummyInstallationInventory : IInstallationInventory
    {
        public List<IItemStack> insertedItems = new List<IItemStack>();

        public DummyInstallationInventory()
        {
            insertedItems = ItemStackFactory.CreateEmptyItemStacksArray(100).ToList();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < insertedItems.Count; i++)
            {
                if (!insertedItems[i].CanAdd(itemStack)) continue;
                var r = insertedItems[i].AddItem(itemStack);
                insertedItems[i] = r.MineItemStack;
                return r.ReceiveItemStack;
            }
            insertedItems.Sort((i,j) => i.Id - j.Id);
            return new NullItemStack();
        }
    }
}