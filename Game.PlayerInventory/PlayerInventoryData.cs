using System;
using System.Collections.Generic;
using Core.Item;

namespace PlayerInventory
{
    public class PlayerInventoryData
    {
        //TODO インベントリに必要な機能
        //TODO インベントリからアイテムを取る
        //TODO インベントリにアイテムを入れる
        
        public readonly int PlayerId;
        private readonly List<IItemStack> MainInventory;    

        public PlayerInventoryData(int playerId)
        {
            PlayerId = playerId;
            MainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                MainInventory.Add(new NullItemStack());
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
                MainInventory[index] = result.MineItemStack;
                return result.ReceiveItemStack;
            }
            else
            {
                var oldItemStack = MainInventory[index];
                MainInventory[index] = itemStack;
                return oldItemStack;
            }
        }
        
        public IItemStack DropItem(int index, int count)
        {
            return new NullItemStack();
        }
        

        public IItemStack UseHotbarItem(int index)
        {
            return new NullItemStack();
        }
        
        public IItemStack GetItem(int index)
        {
            return new NullItemStack();
        }
        
    }
}