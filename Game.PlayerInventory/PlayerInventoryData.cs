using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using PlayerInventory.Event;

namespace PlayerInventory
{
    public class PlayerInventoryData : IInventory
    {
        private readonly int _playerId;
        private readonly List<IItemStack> _mainInventory;
        private readonly PlayerInventoryUpdateEvent _playerInventoryUpdateEvent;
        private readonly ItemStackFactory _itemStackFactory;

        public PlayerInventoryData(int playerId,PlayerInventoryUpdateEvent playerInventoryUpdateEvent, ItemStackFactory itemStackFactory)
        {
            _playerInventoryUpdateEvent = playerInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;

            _playerId = playerId;
            _mainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                _mainInventory.Add(_itemStackFactory.CreatEmpty());
            }
        }
        
        public IItemStack InsertItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= _mainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            if (itemStack.Id == _mainInventory[slot].Id)
            {
                var result = _mainInventory[slot].AddItem(itemStack);
                _mainInventory[slot] = result.ProcessResultItemStack;
                return result.RemainderItemStack;
            }else if( _mainInventory[slot].Count == 0)
            {
                _mainInventory[slot] = itemStack;
                return _itemStackFactory.CreatEmpty();
            }
            
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(_playerId,slot,_mainInventory[slot]));
            return itemStack;
        }
        
        public IItemStack DropItem(int slot, int count)
        {
            if(slot < 0 || slot >= _mainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            var result = _mainInventory[slot].SubItem(count);
            _mainInventory[slot] = result;
            
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(_playerId,slot,_mainInventory[slot]));
            return _itemStackFactory.Create(_mainInventory[slot].Id,count);
        }
        

        public IItemStack UseHotBar(int slot)
        {
            if(slot < 0 || slot >= _mainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }
            var result = _mainInventory[slot].SubItem(1);
            _mainInventory[slot] = result;
            
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(_playerId,slot,_mainInventory[slot]));
            return _itemStackFactory.Create(_mainInventory[slot].Id, 1);
        }
        
        public IItemStack GetItem(int slot)
        {
            if (slot < 0 || slot >= _mainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            return _mainInventory[slot];
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= _mainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }

            _mainInventory[slot] = itemStack;
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(_playerId,slot,_mainInventory[slot]));
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            if (slot < 0 || slot >= _mainInventory.Count)
            {
                throw new IndexOutOfRangeException();
            }
            //アイテムIDが同じの時はスタックして余ったものを返す
            var item = _mainInventory[slot];
            if (item.Id == itemStack.Id)
            {
                var result = item.AddItem(itemStack);
                _mainInventory[slot] = result.ProcessResultItemStack;
                _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(_playerId,slot,_mainInventory[slot]));
                return result.RemainderItemStack;
            }

            //違う場合はそのまま入れ替える
            _mainInventory[slot] = itemStack;
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(_playerId,slot,_mainInventory[slot]));
            return item;
        }
    }
}