using System;
using System.Collections.Generic;
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
        private readonly int _blockId;
        private readonly int _entityId;
        private readonly Dictionary<int, FuelSetting> _fuelSettings;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;

        private int _fuelItemId = ItemConst.EmptyItemId;
        private double _remainingFuelTime = 0;

        public VanillaPowerGenerator(int blockId, int entityId, int fuelItemSlot, ItemStackFactory itemStackFactory,
            Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate)
        {
            _blockId = blockId;
            _entityId = entityId;
            _fuelSettings = fuelSettings;
            _blockInventoryUpdate = blockInventoryUpdate as BlockOpenableInventoryUpdateEvent;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,fuelItemSlot);
            GameUpdate.AddUpdateObject(this);
        }

        public VanillaPowerGenerator(int blockId, int entityId, string loadString, int fuelItemSlot,
            ItemStackFactory itemStackFactory, Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate)
        {
            _blockId = blockId;
            _entityId = entityId;
            _fuelSettings = fuelSettings;
            _blockInventoryUpdate = blockInventoryUpdate as BlockOpenableInventoryUpdateEvent;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,fuelItemSlot);
            GameUpdate.AddUpdateObject(this);

            var split = loadString.Split(',');
            _fuelItemId = int.Parse(split[0]);
            _remainingFuelTime = double.Parse(split[1]);

            var slot = 0;
            for (var i = 2; i < split.Length; i += 2)
            {
                _itemDataStoreService.SetItem(slot,itemStackFactory.Create(int.Parse(split[i]), int.Parse(split[i + 1])));
                slot++;
            }
        }

        public string GetSaveState()
        {
            //フォーマット
            //_fuelItemId,_remainingFuelTime,_fuelItemId1,_fuelItemCount1,_fuelItemId2,_fuelItemCount2,_fuelItemId3,_fuelItemCount3...
            var saveState = $"{_fuelItemId},{_remainingFuelTime}";
            foreach (var itemStack in _itemDataStoreService.Inventory)
            {
                saveState += $",{itemStack.Id},{itemStack.Count}";
            }

            return saveState;
        }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return _itemDataStoreService.ReplaceItem(slot, itemId, count); }

        public IItemStack InsertItem(IItemStack itemStack) { return _itemDataStoreService.InsertItem(itemStack); }

        public IItemStack InsertItem(int itemId, int count) { return _itemDataStoreService.InsertItem(itemId, count); }


        public void Update()
        {
            //現在燃料を消費しているか判定
            //燃料が在る場合は燃料残り時間をUpdate時間分減らす
            if (_fuelItemId != ItemConst.EmptyItemId)
            {
                _remainingFuelTime -= GameUpdate.UpdateTime;

                //残り時間が0以下の時は燃料の設定をNullItemIdにする
                if (_remainingFuelTime <= 0)
                {
                    _fuelItemId = ItemConst.EmptyItemId;
                }

                return;
            }

            //燃料がない場合はスロットに燃料が在るか判定する
            //スロットに燃料がある場合は燃料の設定し、アイテムを1個減らす
            for (var i = 0; i < _itemDataStoreService.GetSlotSize(); i++)
            {
                //スロットに燃料がある場合
                var slotId = _itemDataStoreService.Inventory[i].Id;
                if (!_fuelSettings.ContainsKey(slotId)) continue;
                
                //ID、残り時間を設定
                _fuelItemId = _fuelSettings[slotId].ItemId;
                _remainingFuelTime = _fuelSettings[slotId].Time;
                
                //アイテムを1個減らす
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
                _entityId, slot + _itemDataStoreService.GetSlotSize(), itemStack));
        }


        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            throw new Exception("発電機にアイテム出力スロットはありません");
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            throw new Exception("発電機にアイテム出力スロットはありません");
        }

        public IItemStack GetItem(int slot) { return _itemDataStoreService.GetItem(slot);}
        public void SetItem(int slot, IItemStack itemStack) { _itemDataStoreService.SetItem(slot,itemStack); }
        public void SetItem(int slot, int itemId, int count) { _itemDataStoreService.SetItem(slot, itemId, count); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _itemDataStoreService.ReplaceItem(slot, itemStack); }

        public int GetSlotSize() { return _itemDataStoreService.GetSlotSize(); }
        
        
        public int GetEntityId() { return _entityId; }

        public int GetBlockId() { return _blockId; }
    }
}