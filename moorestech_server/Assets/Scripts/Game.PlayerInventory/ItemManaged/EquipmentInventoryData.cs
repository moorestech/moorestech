using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;

namespace Game.PlayerInventory.ItemManaged
{
    public class EquipmentInventoryData : IEquipmentInventory, ISortExcludedSlots
    {
        public IReadOnlyList<IItemStack> InventoryItems => _openableInventoryService.InventoryItems;
        public int SelectedEquipmentIndex { get; private set; }

        /// <summary>
        ///     装備は選択インデックスがスロット位置を指すため、整理で詰め直されると別のツールを選ぶことになる
        ///     The selected index points at a slot, so re-packing by sorting would silently select a different tool
        /// </summary>
        public IReadOnlyCollection<int> SortExcludedSlots => Enumerable.Range(0, GetSlotSize()).ToList();

        private readonly EquipmentInventoryUpdateEvent _equipmentInventoryUpdateEvent;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;
        private readonly int _playerId;

        public EquipmentInventoryData(int playerId, EquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent)
        {
            _playerId = playerId;
            _equipmentInventoryUpdateEvent = equipmentInventoryUpdateEvent;

            // スロット数はマスタの装備スロット数に従う
            // The slot count follows the equipment slot count from master
            _openableInventoryService = new OpenableInventoryItemDataStoreService(
                InvokeEvent, ServerContext.ItemStackFactory,
                MasterHolder.ItemMaster.Items.EquipmentSlotCount);

            // 初期選択は既定スロット
            // The initial selection is the default slot
            ApplySelectedEquipmentIndexWithoutEvent(IEquipmentInventory.DefaultSelectedIndex);
        }

        public void SetSelectedEquipmentIndex(int index)
        {
            // サーバー確定値は同値要求にも毎回応答し、クライアントの推測値を収束させる
            // Echo the authoritative value for every request, including equal values, so client speculation converges
            ApplySelectedEquipmentIndexWithoutEvent(index);
            _equipmentInventoryUpdateEvent.OnSelectedEquipmentIndexUpdateInvoke(
                new EquipmentSelectedIndexUpdateEventProperties(_playerId, SelectedEquipmentIndex));
        }

        private void ApplySelectedEquipmentIndexWithoutEvent(int index)
        {
            // 先頭からスロット末尾までにクランプする（未装備という状態は持たない）
            // Clamp between the first and the last slot; there is no unequipped state
            SelectedEquipmentIndex = Math.Clamp(index, 0, GetSlotSize() - 1);
        }

        public IItemStack GetSelectedItem()
        {
            return GetItem(SelectedEquipmentIndex);
        }

        /// <summary>
        ///     セーブから装備とその選択位置を復元し、スロットに入り切らなかった分を返す。
        ///     Restore the equipment items and the selected index from a save, returning what did not fit.
        /// </summary>
        public List<IItemStack> RestoreFromSave(List<IItemStack> savedItems, int selectedEquipmentIndex)
        {
            var overflowItems = SetItemsWithoutEvent(savedItems);

            // 復元はアイテムも選択も無発火で揃え、ロード時に差分イベントを積まない
            // Restoring keeps both items and selection event-free so loading queues no diff events
            ApplySelectedEquipmentIndexWithoutEvent(selectedEquipmentIndex);

            return overflowItems;
        }

        /// <summary>
        ///     スロット順のアイテム列を無発火で入れ、入り切らなかった分を返す。復元と初期装備投入が共有する
        ///     Sets a slot-ordered item list without events and returns what did not fit; shared by restore and the initial grant
        /// </summary>
        public List<IItemStack> SetItemsWithoutEvent(List<IItemStack> items)
        {
            // スロット数はマスタ由来で保存されないため、マスタが縮んだ場合は入る分だけ入れる
            // The slot count comes from master and is not saved, so only what fits is set when master shrank
            var setCount = Math.Min(items.Count, GetSlotSize());
            for (var slot = 0; slot < setCount; slot++)
                _openableInventoryService.SetItemWithoutEvent(slot, items[slot]);

            // あふれた装備は捨てずに呼び出し元へ返し、プレイヤーのアイテム消失を防ぐ
            // Overflowing equipment is handed back to the caller instead of dropped so the player loses nothing
            var overflowItems = new List<IItemStack>();
            for (var index = setCount; index < items.Count; index++)
            {
                if (items[index].Count == 0) continue;
                overflowItems.Add(items[index]);
            }

            return overflowItems;
        }

        public IItemStack GetItem(int slot)
        {
            return _openableInventoryService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _openableInventoryService.SetItem(slot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            _openableInventoryService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return _openableInventoryService.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            return _openableInventoryService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _openableInventoryService.InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return _openableInventoryService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _openableInventoryService.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _openableInventoryService.InsertionCheck(itemStacks);
        }

        public int GetSlotSize()
        {
            return _openableInventoryService.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return _openableInventoryService.CreateCopiedItems();
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _equipmentInventoryUpdateEvent.OnInventoryUpdateInvoke(new PlayerInventoryUpdateEventProperties(
                _playerId, slot, itemStack));
        }
    }
}
