using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Core.Inventory
{
    public class OpenableInventorySlotIndex
    {
        public IReadOnlyList<int> NonEmptySlotIndexes => _nonEmptySlots.Slots;
        public bool HasInsertableSlot => 0 < _emptySlots.Count || 0 < _partialSlotCount;
        public int Version { get; private set; }

        private readonly InventorySlotList _nonEmptySlots = new();
        private readonly InventorySlotList _emptySlots = new();
        private readonly Dictionary<ItemId, InventorySlotList> _partialSlotsByItemId = new();
        private readonly Dictionary<ItemId, int> _slotCountByItemId = new();
        private int _partialSlotCount;

        public OpenableInventorySlotIndex(int slotCount)
        {
            for (var i = 0; i < slotCount; i++) _emptySlots.Add(i);
        }

        public void Rebuild(IReadOnlyList<IItemStack> inventoryItems)
        {
            _nonEmptySlots.Clear();
            _emptySlots.Clear();
            _partialSlotsByItemId.Clear();
            _slotCountByItemId.Clear();
            _partialSlotCount = 0;

            // 既存アルゴリズムで変更されたslot状態をまとめて再構築する
            // Rebuild slot state after low-frequency legacy mutation paths
            for (var i = 0; i < inventoryItems.Count; i++)
            {
                AddSlotState(i, inventoryItems[i]);
            }

            Version++;
        }

        public void SetSlot(int slot, IItemStack oldItemStack, IItemStack newItemStack)
        {
            RemoveSlotState(slot, oldItemStack);
            AddSlotState(slot, newItemStack);
            Version++;
        }

        public bool CanInsertItem(IItemStack itemStack, OpenableInventoryItemDataStoreServiceOption option)
        {
            if (IsEmpty(itemStack)) return true;

            // 既存同種がある場合、設定次第で空slotへの新規スタックを禁止する
            // Existing same items can forbid creating a new stack depending on options
            var hasSameItem = _slotCountByItemId.ContainsKey(itemStack.Id);
            if (HasPartialSlot(itemStack.Id)) return true;
            if (!option.AllowMultipleStacksPerItemOnInsert && hasSameItem) return false;
            return 0 < _emptySlots.Count;
        }

        public IItemStack InsertItem(IItemStack itemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option, System.Action<int> onSlotUpdate)
        {
            if (IsEmpty(itemStack)) return itemStack;

            // 既存スタックを優先し、残った場合だけ空slotを使う
            // Prefer existing stacks and use an empty slot only for the remainder
            var hasSameItem = _slotCountByItemId.ContainsKey(itemStack.Id);
            var currentItemStack = InsertToPartialSlots(itemStack, inventoryItems, onSlotUpdate);
            if (IsEmpty(currentItemStack)) return currentItemStack;
            if (!option.AllowMultipleStacksPerItemOnInsert && hasSameItem) return currentItemStack;
            return InsertToEmptySlot(currentItemStack, inventoryItems, onSlotUpdate);
        }

        private IItemStack InsertToPartialSlots(IItemStack itemStack, List<IItemStack> inventoryItems, System.Action<int> onSlotUpdate)
        {
            if (!_partialSlotsByItemId.TryGetValue(itemStack.Id, out var slots)) return itemStack;

            // indexの末尾を使い、満杯になったslotを即座に候補から外す
            // Use the tail candidate and remove filled slots immediately
            var currentItemStack = itemStack;
            var skippedSlots = new List<int>();
            while (!IsEmpty(currentItemStack) && 0 < slots.Count)
            {
                var slot = slots.GetLast();
                var remainingItemStack = InsertToSlot(slot, currentItemStack, inventoryItems, onSlotUpdate);
                if (remainingItemStack.Count == currentItemStack.Count && remainingItemStack.Id == currentItemStack.Id)
                {
                    slots.Remove(slot);
                    skippedSlots.Add(slot);
                    continue;
                }

                currentItemStack = remainingItemStack;
            }

            foreach (var skippedSlot in skippedSlots) slots.Add(skippedSlot);

            return currentItemStack;
        }

        private IItemStack InsertToEmptySlot(IItemStack itemStack, List<IItemStack> inventoryItems, System.Action<int> onSlotUpdate)
        {
            if (_emptySlots.Count == 0) return itemStack;
            return InsertToSlot(_emptySlots.GetLast(), itemStack, inventoryItems, onSlotUpdate);
        }

        private IItemStack InsertToSlot(int slot, IItemStack itemStack, List<IItemStack> inventoryItems, System.Action<int> onSlotUpdate)
        {
            var oldItemStack = inventoryItems[slot];
            if (!oldItemStack.IsAllowedToAddWithRemain(itemStack)) return itemStack;

            // 実際のAddItemを使い、既存のスタック演算と余り計算を維持する
            // Use the existing stack operation to preserve remainder calculation
            var result = oldItemStack.AddItem(itemStack);
            if (!oldItemStack.Equals(result.ProcessResultItemStack))
            {
                inventoryItems[slot] = result.ProcessResultItemStack;
                SetSlot(slot, oldItemStack, result.ProcessResultItemStack);
                onSlotUpdate(slot);
            }

            return result.RemainderItemStack;
        }

        private void AddSlotState(int slot, IItemStack itemStack)
        {
            if (IsEmpty(itemStack))
            {
                _emptySlots.Add(slot);
                return;
            }

            _nonEmptySlots.Add(slot);
            _slotCountByItemId[itemStack.Id] = GetItemSlotCount(itemStack.Id) + 1;
            if (!IsPartial(itemStack)) return;
            GetPartialSlots(itemStack.Id).Add(slot);
            _partialSlotCount++;
        }

        private void RemoveSlotState(int slot, IItemStack itemStack)
        {
            if (IsEmpty(itemStack))
            {
                _emptySlots.Remove(slot);
                return;
            }

            _nonEmptySlots.Remove(slot);
            ReduceItemSlotCount(itemStack.Id);
            if (!IsPartial(itemStack)) return;
            _partialSlotsByItemId[itemStack.Id].Remove(slot);
            _partialSlotCount--;
        }

        private InventorySlotList GetPartialSlots(ItemId itemId)
        {
            if (!_partialSlotsByItemId.TryGetValue(itemId, out var slots))
            {
                slots = new InventorySlotList();
                _partialSlotsByItemId[itemId] = slots;
            }

            return slots;
        }

        private bool HasPartialSlot(ItemId itemId)
        {
            return _partialSlotsByItemId.TryGetValue(itemId, out var slots) && 0 < slots.Count;
        }

        private int GetItemSlotCount(ItemId itemId)
        {
            return _slotCountByItemId.GetValueOrDefault(itemId);
        }

        private void ReduceItemSlotCount(ItemId itemId)
        {
            var count = _slotCountByItemId[itemId] - 1;
            if (count == 0) _slotCountByItemId.Remove(itemId);
            else _slotCountByItemId[itemId] = count;
        }

        private static bool IsEmpty(IItemStack itemStack)
        {
            return itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count == 0;
        }

        private static bool IsPartial(IItemStack itemStack)
        {
            return itemStack.Count < MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).MaxStack;
        }
    }
}
