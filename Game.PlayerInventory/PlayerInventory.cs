using System;
using System.Collections.Generic;
using Core.Item;

namespace PlayerInventory
{
    public class PlayerInventory
    {
        //TODO インベントリに必要な機能
        //TODO インベントリからアイテムを取る
        //TODO インベントリにアイテムを入れる
        
        public readonly int PlayerId;
        private readonly List<IItemStack> MainInventory;    

        public PlayerInventory(int playerId)
        {
            PlayerId = playerId;
            MainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                MainInventory.Add(new NullItemStack());
            }
        }
        
        public IItemStack SetItem(int index, IItemStack itemStack)
        {
            return new NullItemStack();
        }

        public bool UseHotbarItem(int index)
        {
            return false;
        }
        
        public IItemStack GetItem(int index)
        {
            return new NullItemStack();
        }
        
    }
}