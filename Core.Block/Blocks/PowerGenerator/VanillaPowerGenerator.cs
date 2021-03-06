using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Block.BlockInventory;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.Const;
using Core.Electric;
using Core.Inventory;
using Core.Item;
using Core.Update;

namespace Core.Block.Blocks.PowerGenerator
{
    public class VanillaPowerGenerator : IBlock, IPowerGenerator, IBlockInventory, IUpdate,IOpenableInventory
    {       
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        
        private readonly Dictionary<int, FuelSetting> _fuelSettings;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;

        private int _fuelItemId = ItemConst.EmptyItemId;
        private double _remainingFuelTime = 0;

        public VanillaPowerGenerator(int blockId, int entityId, ulong blockHash, int fuelItemSlot, ItemStackFactory itemStackFactory,
            Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate)
        {
            BlockId = blockId;
            EntityId = entityId;
            _fuelSettings = fuelSettings;
            BlockHash = blockHash;
            _blockInventoryUpdate = blockInventoryUpdate as BlockOpenableInventoryUpdateEvent;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,fuelItemSlot);
            GameUpdate.AddUpdateObject(this);
        }

        public VanillaPowerGenerator(int blockId, int entityId, ulong blockHash, string loadString, int fuelItemSlot,
            ItemStackFactory itemStackFactory, Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate)
        {
            BlockId = blockId;
            EntityId = entityId;
            _fuelSettings = fuelSettings;
            BlockHash = blockHash;
            _blockInventoryUpdate = blockInventoryUpdate as BlockOpenableInventoryUpdateEvent;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,fuelItemSlot);
            GameUpdate.AddUpdateObject(this);

            var split = loadString.Split(',');
            _fuelItemId = int.Parse(split[0]);
            _remainingFuelTime = double.Parse(split[1]);

            var slot = 0;
            for (var i = 2; i < split.Length; i += 2)
            {
                _itemDataStoreService.SetItem(slot,itemStackFactory.Create(ulong.Parse(split[i]), int.Parse(split[i + 1])));
                slot++;
            }
        }

        public ReadOnlyCollection<IItemStack> Items => _itemDataStoreService.Items;

        public string GetSaveState()
        {
            //??????????????????
            //_fuelItemId,_remainingFuelTime,_fuelItemId1,_fuelItemCount1,_fuelItemId2,_fuelItemCount2,_fuelItemId3,_fuelItemCount3...
            var saveState = $"{_fuelItemId},{_remainingFuelTime}";
            foreach (var itemStack in _itemDataStoreService.Inventory)
            {
                saveState += $",{itemStack.ItemHash},{itemStack.Count}";
            }

            return saveState;
        }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _itemDataStoreService.ReplaceItem(slot, itemId, count); }

        public IItemStack InsertItem(IItemStack itemStack) { return _itemDataStoreService.InsertItem(itemStack); }

        public IItemStack InsertItem(int itemId, int count) { return _itemDataStoreService.InsertItem(itemId, count); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { return _itemDataStoreService.InsertItem(itemStacks); }

        public bool InsertionCheck(List<IItemStack> itemStacks) { return _itemDataStoreService.InsertionCheck(itemStacks); }


        public void Update()
        {
            //??????????????????????????????????????????
            //?????????????????????????????????????????????Update??????????????????
            if (_fuelItemId != ItemConst.EmptyItemId)
            {
                _remainingFuelTime -= GameUpdate.UpdateTime;

                //???????????????0?????????????????????????????????NullItemId?????????
                if (_remainingFuelTime <= 0)
                {
                    _fuelItemId = ItemConst.EmptyItemId;
                }

                return;
            }

            //?????????????????????????????????????????????????????????????????????
            //???????????????????????????????????????????????????????????????????????????1????????????
            for (var i = 0; i < _itemDataStoreService.GetSlotSize(); i++)
            {
                //????????????????????????????????????
                var slotItemId = _itemDataStoreService.Inventory[i].Id;
                if (!_fuelSettings.ContainsKey(slotItemId)) continue;
                
                //ID????????????????????????
                _fuelItemId = _fuelSettings[slotItemId].ItemId;
                _remainingFuelTime = _fuelSettings[slotItemId].Time;
                
                //???????????????1????????????
                _itemDataStoreService.SetItem(i,_itemDataStoreService.Inventory[i].SubItem(1));
                return;
            }
        }

        public int OutputPower()
        {
            if (_fuelSettings.ContainsKey(_fuelItemId))
            {
                return _fuelSettings[_fuelItemId].Power;
            }

            return 0;
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                EntityId, slot, itemStack));
        }


        //?????????????????????????????????????????????
        public void AddOutputConnector(IBlockInventory blockInventory) { }
        public void RemoveOutputConnector(IBlockInventory blockInventory) { }

        public IItemStack GetItem(int slot) { return _itemDataStoreService.GetItem(slot);}
        public void SetItem(int slot, IItemStack itemStack) { _itemDataStoreService.SetItem(slot,itemStack); }
        public void SetItem(int slot, int itemId, int count) { _itemDataStoreService.SetItem(slot, itemId, count); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _itemDataStoreService.ReplaceItem(slot, itemStack); }

        public int GetSlotSize() { return _itemDataStoreService.GetSlotSize(); }
    }
}