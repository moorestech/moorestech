using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Installation;
using industrialization.Core.Item;
using industrialization.Core.Util;

namespace industrialization.Core.Test
{
    public class DummyInstallationInventory : IInstallationInventory
    {
        public bool IsItemExists => _isItemExists;
        private bool _isItemExists = false;
        
        public List<IItemStack> insertedItems = new List<IItemStack>();
        private int InsertToEndNum { get; }
        private int _endInsertCnt;

        public DummyInstallationInventory(int insertToEndNum = 1)
        {
            _isItemExists = false;
            this.InsertToEndNum = insertToEndNum;
            _endInsertCnt = 0;
            insertedItems = CreateEmptyItemStacksList.Create(100).ToList();
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < insertedItems.Count; i++)
            {
                if (!insertedItems[i].IsAllowedToAdd(itemStack)) continue;
                var r = insertedItems[i].AddItem(itemStack);
                insertedItems[i] = r.MineItemStack;
                _endInsertCnt++;
                _isItemExists = InsertToEndNum <= _endInsertCnt;
                return r.ReceiveItemStack;
            }
            return new NullItemStack();
        }

        public void ChangeConnector(IInstallationInventory installationInventory)
        {
        }
    }
}