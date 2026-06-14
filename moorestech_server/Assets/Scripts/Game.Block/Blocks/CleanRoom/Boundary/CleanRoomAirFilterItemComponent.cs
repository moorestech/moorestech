using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // フィルタースロット。filterItemId のアイテムだけをフィルターとして数え・消費する。
    // Filter slots; only items matching filterItemId are counted/consumed as filters.
    public class CleanRoomAirFilterItemComponent : IOpenableBlockInventoryComponent, IBlockSaveState
    {
        public string SaveKey => "cleanRoomAirFilterItem";
        public bool IsDestroy { get; private set; }

        public bool HasFilter => FilterCount > 0;
        public IReadOnlyList<IItemStack> InventoryItems => _inventoryService.InventoryItems;

        private readonly OpenableInventoryItemDataStoreService _inventoryService;
        private readonly ItemId _filterItemId;
        private readonly BlockInstanceId _blockInstanceId;

        internal OpenableInventoryItemDataStoreService InventoryService => _inventoryService;

        public CleanRoomAirFilterItemComponent(int slotCount, ItemId filterItemId, BlockInstanceId blockInstanceId)
        {
            _filterItemId = filterItemId;
            _blockInstanceId = blockInstanceId;
            _inventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, Math.Max(1, slotCount));
        }

        public CleanRoomAirFilterItemComponent(Dictionary<string, string> componentStates, int slotCount, ItemId filterItemId, BlockInstanceId blockInstanceId)
            : this(slotCount, filterItemId, blockInstanceId)
        {
            if (!componentStates.TryGetValue(SaveKey, out var stateRaw)) return;
            var items = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(stateRaw);
            RestoreItems(items);
        }

        // フィルターアイテムだけを数える（誤投入アイテムは無視）。
        // Count only filter items; foreign items are ignored.
        public int FilterCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _inventoryService.GetSlotSize(); i++)
                {
                    var item = _inventoryService.GetItem(i);
                    if (item.Id == _filterItemId) count += item.Count;
                }
                return count;
            }
        }

        // フィルターを1個消費する。フィルターアイテム以外は消費しない。
        // Consume exactly one filter; never consumes non-filter items.
        public bool TryConsumeOneFilter()
        {
            BlockException.CheckDestroy(this);
            for (var i = 0; i < _inventoryService.GetSlotSize(); i++)
            {
                var item = _inventoryService.GetItem(i);
                if (item.Id != _filterItemId || item.Count <= 0) continue;
                _inventoryService.SetItem(i, item.SubItem(1));
                return true;
            }
            return false;
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

        public void Destroy()
        {
            IsDestroy = true;
        }

        #region IOpenableBlockInventoryComponent 委譲 / delegation

        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.InsertItem(itemStack);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            return InsertItem(itemStack);
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

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);
            return _inventoryService.ReplaceItem(slot, itemId, count);
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

        #endregion

        // セーブデータからスロットを復元（ロード時はイベント抑止）。
        // Restore slots from save data without firing events during load.
        internal void RestoreItems(List<ItemStackSaveJsonObject> items)
        {
            if (items == null) return;
            var slotSize = _inventoryService.GetSlotSize();
            for (var i = 0; i < Math.Min(slotSize, items.Count); i++)
            {
                var stack = items[i]?.ToItemStack();
                if (stack == null) continue;
                _inventoryService.SetItemWithoutEvent(i, stack);
            }
        }

        // インベントリ更新イベントを発火させ、クライアントへ同期させる。ブロック未登録時はスキップ。
        // Fire the inventory update event to synchronise clients; skip if block is not yet registered.
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            if (IsDestroy) return;
            if (ServerContext.WorldBlockDatastore.GetBlock(_blockInstanceId) == null) return;
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var properties = new BlockOpenableInventoryUpdateEventProperties(_blockInstanceId, slot, itemStack);
            blockInventoryUpdate.OnInventoryUpdateInvoke(properties);
        }
    }
}
