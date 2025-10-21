using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Gear
{
    // アイテムスロットを管理し、他コンポーネントへインベントリ機能を提供する
    // Manage item slots and expose inventory functionality to other components
    public class SteamGearGeneratorItemComponent : IBlockInventory, IOpenableInventory, IBlockSaveState
    {
        public IReadOnlyList<IItemStack> InventoryItems => _inventoryService.InventoryItems;
        public string SaveKey => "steamGearGeneratorItem";
        
        
        private readonly OpenableInventoryItemDataStoreService _inventoryService;
        private readonly BlockInstanceId _blockInstanceId;
        
        internal OpenableInventoryItemDataStoreService InventoryService => _inventoryService;

        public SteamGearGeneratorItemComponent(
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId)
        {
            _blockInstanceId = blockInstanceId;
            var slotCount = Math.Max(0, param.FuelItemSlotCount);
            _inventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotCount);
        }

        public SteamGearGeneratorItemComponent(
            Dictionary<string, string> componentStates,
            SteamGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId)
            : this(param, blockInstanceId)
        {
            if (!componentStates.TryGetValue(SaveKey, out var stateRaw)) return;
            var items = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(stateRaw);
            RestoreItems(items);
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var slotSize = _inventoryService.GetSlotSize();
            var serialized = new List<ItemStackSaveJsonObject>(slotSize);
            for (var i = 0; i < slotSize; i++)
            {
                serialized.Add(new ItemStackSaveJsonObject(_inventoryService.GetItem(i)));
            }

            return JsonConvert.SerializeObject(serialized);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemStack);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertionCheck(itemStacks);
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            _inventoryService.SetItem(slot, itemStack);
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.CreateCopiedItems();
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemStacks);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            _inventoryService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.ReplaceItem(slot, itemStack);
        }

        // 現在のスロット内容をまとめて復元するためのユーティリティ
        // Utility helper used to restore stored slot contents in batch
        internal void RestoreItems(List<ItemStackSaveJsonObject> items)
        {
            if (items == null) return;
            var slotSize = _inventoryService.GetSlotSize();
            for (var i = 0; i < Math.Min(slotSize, items.Count); i++)
            {
                var stack = items[i]?.ToItemStack();
                if (stack == null) continue;
                _inventoryService.SetItemWithoutEvent(i, stack);
                InvokeEvent(i, stack);
            }
        }

        // インベントリ更新イベントを発火させ、クライアントへ同期させる
        // Fire the inventory update event to synchronise clients
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            if (IsDestroy) return;
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var properties = new BlockOpenableInventoryUpdateEventProperties(_blockInstanceId, slot, itemStack);
            blockInventoryUpdate.OnInventoryUpdateInvoke(properties);
        }
        
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        public bool IsDestroy { get; private set; }
    }
}
