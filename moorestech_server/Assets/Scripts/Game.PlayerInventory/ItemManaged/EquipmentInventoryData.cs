using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;

namespace Game.PlayerInventory.ItemManaged
{
    public class EquipmentInventoryData : IEquipmentInventory, IItemAcceptanceInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems => _openableInventoryService.InventoryItems;
        public int SelectedEquipmentIndex { get; private set; }

        private readonly EquipmentInventoryUpdateEvent _equipmentInventoryUpdateEvent;
        private readonly OpenableInventoryItemDataStoreService _openableInventoryService;
        private readonly int _playerId;

        public EquipmentInventoryData(int playerId, EquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent)
        {
            _playerId = playerId;
            _equipmentInventoryUpdateEvent = equipmentInventoryUpdateEvent;

            // 受入制限は自身が持つため、自分をoptionへ渡してCore側の強制に乗せる
            // This inventory owns the acceptance rule, so pass itself into the option to ride Core's enforcement
            _openableInventoryService = new OpenableInventoryItemDataStoreService(
                InvokeEvent, ServerContext.ItemStackFactory,
                MasterHolder.ToolMaster.EquipmentSlotCount,
                new OpenableInventoryItemDataStoreServiceOption(this));

            // 初期選択は先頭スロットだが、装備スロットが無いマスタでは素手へ丸める
            // The initial selection is the first slot, clamped to bare hands when master has no equipment slot
            ApplySelectedEquipmentIndexWithoutEvent(0);
        }

        public bool CanAccept(ItemId itemId)
        {
            return MasterHolder.ToolMaster.IsTool(itemId);
        }

        public int GetMaxCountPerSlot(ItemId itemId)
        {
            return 1;
        }

        public void SetSelectedEquipmentIndex(int index)
        {
            // クランプ後の値が変化した時だけ通知する
            // Notify only when the clamped value actually changes
            var previousIndex = SelectedEquipmentIndex;
            ApplySelectedEquipmentIndexWithoutEvent(index);
            if (previousIndex == SelectedEquipmentIndex) return;

            _equipmentInventoryUpdateEvent.OnSelectedEquipmentIndexUpdateInvoke(
                new EquipmentSelectedIndexUpdateEventProperties(_playerId, SelectedEquipmentIndex));
        }

        private void ApplySelectedEquipmentIndexWithoutEvent(int index)
        {
            // -1(素手)からスロット末尾までにクランプする
            // Clamp between -1 (bare hands) and the last slot
            SelectedEquipmentIndex = Math.Clamp(index, -1, GetSlotSize() - 1);
        }

        public IItemStack GetSelectedItem()
        {
            // 素手のときは空スタックを返す
            // Return an empty stack when bare hands are selected
            return SelectedEquipmentIndex < 0 ? ServerContext.ItemStackFactory.CreatEmpty() : GetItem(SelectedEquipmentIndex);
        }

        /// <summary>
        ///     セーブから装備を復元し、装備できなかったアイテムを返す。
        ///     復元はSetItemWithoutEventで書き込むため受入制限が効かず、ここで明示的に検証する。
        ///     Restore equipment from a save and return the items that could not be equipped.
        ///     Restoring writes through SetItemWithoutEvent, which skips acceptance, so it is verified explicitly here.
        /// </summary>
        public List<IItemStack> RestoreFromSave(List<IItemStack> savedItems, int selectedEquipmentIndex)
        {
            var rejectedItems = new List<IItemStack>();
            for (var slot = 0; slot < savedItems.Count; slot++)
            {
                // スロット数はマスタ由来で保存されないため、マスタが縮んだ分のセーブは丸ごと退避する
                // The slot count comes from master and is not saved, so stacks beyond it are handed back whole
                if (GetSlotSize() <= slot)
                {
                    AddRejectedItem(savedItems[slot]);
                    continue;
                }
                RestoreSlot(slot, savedItems[slot]);
            }

            // 復元はアイテムも選択も無発火で揃え、ロード時に差分イベントを積まない
            // Restoring keeps both items and selection event-free so loading queues no diff events
            ApplySelectedEquipmentIndexWithoutEvent(selectedEquipmentIndex);
            return rejectedItems;

            #region Internal

            void RestoreSlot(int slot, IItemStack savedItem)
            {
                // ツールでないアイテムは装備させず丸ごと退避する
                // Items that are not tools are never equipped and are handed back whole
                if (!CanAccept(savedItem.Id))
                {
                    AddRejectedItem(savedItem);
                    return;
                }

                // 1枠上限までを装備し、超過分だけを退避する
                // Equip up to the per-slot cap and hand back only the excess
                var maxCountPerSlot = GetMaxCountPerSlot(savedItem.Id);
                if (savedItem.Count <= maxCountPerSlot)
                {
                    _openableInventoryService.SetItemWithoutEvent(slot, savedItem);
                    return;
                }

                _openableInventoryService.SetItemWithoutEvent(slot, savedItem.SubItem(savedItem.Count - maxCountPerSlot));
                AddRejectedItem(savedItem.SubItem(maxCountPerSlot));
            }

            void AddRejectedItem(IItemStack rejectedItem)
            {
                if (rejectedItem.Count == 0) return;
                rejectedItems.Add(rejectedItem);
            }

            #endregion
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
