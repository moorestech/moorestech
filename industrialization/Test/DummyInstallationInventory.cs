using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Installation;
using industrialization.Core.Item;
using industrialization.Core.Util;

namespace industrialization.Installation
{
    public class DummyInstallationInventory : IInstallationInventory
    {
        public static bool IsFinish => _isFinish;
        private static bool _isFinish = false;
        
        public List<IItemStack> insertedItems = new List<IItemStack>();
        private int InsertToEndNum { get; }
        private int _endInsertCnt;

        public DummyInstallationInventory(int insertToEndNum = 1)
        {
            _isFinish = false;
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
                _isFinish = InsertToEndNum <= _endInsertCnt;
                return r.ReceiveItemStack;
            }
            insertedItems.Sort((i,j) => i.Id - j.Id);
            _endInsertCnt++;
            _isFinish = InsertToEndNum <= _endInsertCnt;
            return new NullItemStack();
        }
    }
}