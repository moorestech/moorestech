using System;
using System.Collections.Generic;
using Core.Item;
using Core.Item.Implementation;
using Core.Item.Util;

namespace PlayerInventory
{
    public class PlayerInventoryData
    {
        public readonly int PlayerId;
        private readonly List<IItemStack> MainInventory;    

        public PlayerInventoryData(int playerId)
        {
            PlayerId = playerId;
            MainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                MainInventory.Add(ItemStackFactory.CreatEmpty());
            }
        }
        
        public IItemStack InsertItem(int index, IItemStack itemStack)
        {
            if (index < 0 || index >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            if (itemStack.Id == MainInventory[index].Id)
            {
                var result = MainInventory[index].AddItem(itemStack);
                MainInventory[index] = result.ProcessResultItemStack;
                return result.RemainderItemStack;
            }else if( MainInventory[index].Id == ItemConst.NullItemId)
            {
                MainInventory[index] = itemStack;
                return ItemStackFactory.CreatEmpty();
            }
            {
                return itemStack;
            }
        }
        
        public IItemStack DropItem(int index, int count)
        {
            if(index < 0 || index >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            var result = MainInventory[index].SubItem(count);
            MainInventory[index] = result;
            return ItemStackFactory.Create(MainInventory[index].Id,count);
        }
        

        public IItemStack UseHotBar(int index)
        {
            if(index < 0 || index >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }
            var result = MainInventory[index].SubItem(1);
            MainInventory[index] = result;
            return ItemStackFactory.Create(MainInventory[index].Id, 1);
        }
        
        public IItemStack GetItem(int index)
        {
            if (index < 0 || index >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            return MainInventory[index];
        }

        public void SetItem(int index, IItemStack itemStack)
        {
            if (index < 0 || index >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            MainInventory[index] = itemStack;
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            var result = MainInventory[slot].AddItem(itemStack);
            MainInventory[slot] = result.ProcessResultItemStack;
            return result.RemainderItemStack;
        }
    }
}