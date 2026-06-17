using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Newtonsoft.Json;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.Chest
{
    public class VanillaChestComponent : IOpenableBlockInventoryComponent, IBlockSaveState, IUpdatableBlockComponent, IBlockInventoryFastInsertTarget
    {
        public IReadOnlyList<IItemStack> InventoryItems => _itemDataStoreService.InventoryItems;
        public IReadOnlyList<int> NonEmptySlotIndexes => _itemDataStoreService.NonEmptySlotIndexes;
        public bool HasInsertableSlot => _itemDataStoreService.HasInsertableSlot;
        public int InventoryVersion => _itemDataStoreService.InventoryVersion;
        public BlockInstanceId BlockInstanceId { get; }

        private readonly IBlockInventoryInserter _blockInventoryInserter;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        public VanillaChestComponent(BlockInstanceId blockInstanceId, int slotNum, IBlockInventoryInserter blockInventoryInserter)
        {
            BlockInstanceId = blockInstanceId;
            _blockInventoryInserter = blockInventoryInserter;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotNum);
        }

        public VanillaChestComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, int slotNum, IBlockInventoryInserter blockInventoryInserter) :
            this(blockInstanceId, slotNum, blockInventoryInserter)
        {
            var itemJsons = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(componentStates[SaveKey]);
            if (itemJsons == null) return;

            // セーブデータロード中はワールド未登録なのでイベントを発火しない
            // Do not invoke events while loading save data before world registration
            for (var i = 0; i < slotNum; i++)
            {
                if (i >= itemJsons.Count) break;
                _itemDataStoreService.SetItemWithoutEvent(i, itemJsons[i].ToItemStack());
            }

            if (slotNum < itemJsons.Count) Debug.LogError($"保存されているアイテムスロット数がチェストのスロット数を超えています BlockInstanceId:{blockInstanceId}");
        }

        public string SaveKey { get; } = typeof(VanillaChestComponent).FullName;

        public string GetSaveState()
        {
            CheckDestroy(this);

            // 現在のslot順で保存用JSONへ変換する
            // Convert current slots to save JSON in slot order
            var itemJson = new List<ItemStackSaveJsonObject>();
            foreach (var item in _itemDataStoreService.InventoryItems)
            {
                itemJson.Add(new ItemStackSaveJsonObject(item));
            }

            return JsonConvert.SerializeObject(itemJson);
        }

        public void Update()
        {
            CheckDestroy(this);

            // 搬出先が完全に詰まっている場合は搬出元スロットを走査しない
            // Skip source slot scanning when the selected destination is fully locked
            var targetState = _blockInventoryInserter as IBlockInventoryInsertTargetState;
            if (targetState != null && !targetState.CanInsertToNextTarget()) return;

            var nonEmptySlotIndexes = _itemDataStoreService.NonEmptySlotIndexes;
            for (var i = nonEmptySlotIndexes.Count - 1; 0 <= i; i--)
            {
                if (nonEmptySlotIndexes.Count <= i) continue;

                // 非空slotだけを見て、搬出先が対象itemを受けられないなら挿入を試さない
                // Iterate only non-empty slots and avoid insertion attempts for rejected items
                var slot = nonEmptySlotIndexes[i];
                var itemStack = _itemDataStoreService.InventoryItems[slot];
                if (itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count == 0) continue;
                if (targetState != null && !targetState.CanInsertItemToNextTarget(itemStack)) continue;

                var setItem = _blockInventoryInserter.InsertItem(itemStack);
                _itemDataStoreService.SetItem(slot, setItem);
            }
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);

            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(BlockInstanceId, slot, itemStack));
        }

        public void SetItem(int slot, IItemStack itemStack) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemStack); }
        public IItemStack InsertItem(IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.InsertItem(itemStack); }
        public IItemStack InsertItemFast(IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.InsertItemByIndex(itemStack); }
        public bool CanInsertItem(IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.CanInsertItem(itemStack); }
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) { return InsertItem(itemStack); }
        public int GetSlotSize() { CheckDestroy(this); return _itemDataStoreService.GetSlotSize(); }
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { CheckDestroy(this); return _itemDataStoreService.CreateCopiedItems(); }
        public IItemStack GetItem(int slot) { CheckDestroy(this); return _itemDataStoreService.GetItem(slot); }
        public void SetItem(int slot, ItemId itemId, int count) { CheckDestroy(this); _itemDataStoreService.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { CheckDestroy(this); return _itemDataStoreService.ReplaceItem(slot, itemId, count); }
        public IItemStack InsertItem(ItemId itemId, int count) { CheckDestroy(this); return _itemDataStoreService.InsertItem(itemId, count); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { CheckDestroy(this); return _itemDataStoreService.InsertItem(itemStacks); }
        public bool InsertionCheck(List<IItemStack> itemStacks) { CheckDestroy(this); return _itemDataStoreService.InsertionCheck(itemStacks); }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
