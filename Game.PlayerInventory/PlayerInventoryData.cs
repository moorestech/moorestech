using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory
{
    public class PlayerInventoryData : IInventory
    {
        public readonly int PlayerId;
        private readonly List<IItemStack> MainInventory;
        private readonly IPlayerInventoryUpdateEvent _playerInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public PlayerInventoryData(int playerId,IPlayerInventoryUpdateEvent playerInventoryUpdateEvent, ItemStackFactory itemStackFactory)
        {
            _playerInventoryUpdateEvent = playerInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;

            PlayerId = playerId;
            MainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                MainInventory.Add(_itemStackFactory.CreatEmpty());
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
                return _itemStackFactory.CreatEmpty();
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
            return _itemStackFactory.Create(MainInventory[slot].Id,count);
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
            return _itemStackFactory.Create(MainInventory[slot].Id, 1);
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
                _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(PlayerId,slot,MainInventory[slot]));
                return result.RemainderItemStack;
            }

            //違う場合はそのまま入れ替える
            MainInventory[slot] = itemStack;
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(PlayerId,slot,MainInventory[slot]));
            return item;
        }
    }
}