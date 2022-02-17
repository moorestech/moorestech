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

        public PlayerInventoryData(int playerId, PlayerInventoryUpdateEvent playerInventoryUpdateEvent,
            ItemStackFactory itemStackFactory,int slotNumber = PlayerInventoryConst.MainInventorySize)
        {
            _playerInventoryUpdateEvent = playerInventoryUpdateEvent;
            _itemStackFactory = itemStackFactory;

            _playerId = playerId;
            _mainInventory = new List<IItemStack>();
            for (int i = 0; i < slotNumber; i++)
            {
                _mainInventory.Add(_itemStackFactory.CreatEmpty());
            }
        }

        public IItemStack InsertItem(int slot, IItemStack itemStack)
        {
            if (itemStack.Id == _mainInventory[slot].Id)
            {
                var result = _mainInventory[slot].AddItem(itemStack);
                _mainInventory[slot] = result.ProcessResultItemStack;
                return result.RemainderItemStack;
            }
            else if (_mainInventory[slot].Count == 0)
            {
                _mainInventory[slot] = itemStack;
                return _itemStackFactory.CreatEmpty();
            }

            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(
                new PlayerInventoryUpdateEventProperties(_playerId, slot, _mainInventory[slot]));
            return itemStack;
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _mainInventory[slot] = itemStack;
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(
                new PlayerInventoryUpdateEventProperties(_playerId, slot, _mainInventory[slot]));
        }

        public void SetItem(int slot, int itemId, int count) { SetItem(slot, _itemStackFactory.Create(itemId, count)); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            //アイテムIDが同じの時はスタックして余ったものを返す
            var item = _mainInventory[slot];
            if (item.Id == itemStack.Id)
            {
                var result = item.AddItem(itemStack);
                _mainInventory[slot] = result.ProcessResultItemStack;
                _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(
                    new PlayerInventoryUpdateEventProperties(_playerId, slot, _mainInventory[slot]));
                return result.RemainderItemStack;
            }

            //違う場合はそのまま入れ替える
            _mainInventory[slot] = itemStack;
            _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(
                new PlayerInventoryUpdateEventProperties(_playerId, slot, _mainInventory[slot]));
            return item;
        }

        public IItemStack ReplaceItem(int slot, int itemId, int count) { return ReplaceItem(slot, _itemStackFactory.Create(itemId, count)); }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _mainInventory.Count; i++)
            {
                if (!_mainInventory[i].IsAllowedToAdd(itemStack)) continue;

                //インベントリにアイテムを入れる
                var r = _mainInventory[i].AddItem(itemStack);
                _mainInventory[i] = r.ProcessResultItemStack;

                //イベントを発火
                _playerInventoryUpdateEvent.OnPlayerInventoryUpdateInvoke(
                    new PlayerInventoryUpdateEventProperties(_playerId, i, _mainInventory[i]));
                
                //とった結果のアイテムを返す
                return r.RemainderItemStack;
            }

            return itemStack;
        }

        public IItemStack InsertItem(int itemId, int count) { return InsertItem(_itemStackFactory.Create(itemId, count)); }
        public int GetSlotSize() { return _mainInventory.Count; }
        public IItemStack GetItem(int slot) { return _mainInventory[slot]; }
    }
}