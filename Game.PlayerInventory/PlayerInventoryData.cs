using System;
using System.Collections.Generic;
using Core.Item;
using Core.Item.Implementation;
using Core.Item.Util;
using PlayerInventory.Event;

namespace PlayerInventory
{
    public class PlayerInventoryData
    {
        public readonly int PlayerId;
        private readonly List<IItemStack> MainInventory;
        private readonly PlayerInventoryUpdateEvent _playerInventoryUpdateEvent;

        public PlayerInventoryData(int playerId,PlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            _playerInventoryUpdateEvent = playerInventoryUpdateEvent;
            
            PlayerId = playerId;
            MainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                MainInventory.Add(ItemStackFactory.CreatEmpty());
            }
        }
        
        public IItemStack InsertItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            if (itemStack.Id == MainInventory[slot].Id)
            {
                var result = MainInventory[slot].AddItem(itemStack);
                MainInventory[slot] = result.ProcessResultItemStack;
                return result.RemainderItemStack;
            }else if( MainInventory[slot].Id == ItemConst.NullItemId)
            {
                MainInventory[slot] = itemStack;
                return ItemStackFactory.CreatEmpty();
            }
            
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(PlayerId,slot,MainInventory[slot]));
            return itemStack;
        }
        
        public IItemStack DropItem(int slot, int count)
        {
            if(slot < 0 || slot >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            var result = MainInventory[slot].SubItem(count);
            MainInventory[slot] = result;
            
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(PlayerId,slot,MainInventory[slot]));
            return ItemStackFactory.Create(MainInventory[slot].Id,count);
        }
        

        public IItemStack UseHotBar(int slot)
        {
            if(slot < 0 || slot >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }
            var result = MainInventory[slot].SubItem(1);
            MainInventory[slot] = result;
            
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(PlayerId,slot,MainInventory[slot]));
            return ItemStackFactory.Create(MainInventory[slot].Id, 1);
        }
        
        public IItemStack GetItem(int slot)
        {
            if (slot < 0 || slot >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            return MainInventory[slot];
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            MainInventory[slot] = itemStack;
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(PlayerId,slot,MainInventory[slot]));
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= MainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }
            //アイテムIDが同じの時はスタックして余ったものを返す
            var item = MainInventory[slot];
            if (item.Id == itemStack.Id)
            {
                var result = item.AddItem(itemStack);
                MainInventory[slot] = result.ProcessResultItemStack;
                return result.RemainderItemStack;
            }

            //違う場合はそのまま入れ替える
            MainInventory[slot] = itemStack;
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(PlayerId,slot,MainInventory[slot]));
            return item;
        }
    }
}