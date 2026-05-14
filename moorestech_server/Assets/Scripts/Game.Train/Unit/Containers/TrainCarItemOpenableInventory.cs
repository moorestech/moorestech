using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Train.Event;

namespace Game.Train.Unit.Containers
{
    public sealed class TrainCarItemOpenableInventory : IOpenableInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems => _container.InventoryItems.Select(slot => slot.Stack).ToArray();

        private readonly TrainCarInstanceId _trainCarInstanceId;
        private readonly ItemTrainCarContainer _container;
        private readonly Action<TrainInventoryUpdateEventProperties> _onInventoryUpdate;

        public TrainCarItemOpenableInventory(
            TrainCarInstanceId trainCarInstanceId,
            ItemTrainCarContainer container,
            Action<TrainInventoryUpdateEventProperties> onInventoryUpdate)
        {
            _trainCarInstanceId = trainCarInstanceId;
            _container = container;
            _onInventoryUpdate = onInventoryUpdate;
        }

        public IItemStack GetItem(int slot)
        {
            return _container.InventoryItems[slot].Stack;
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (GetItem(slot).Equals(itemStack)) return;

            // 列車コンテナ本体を更新して購読中クライアントへ通知する
            // Update the train container source and notify subscribed clients.
            _container.SetItem(slot, itemStack);
            _onInventoryUpdate(new TrainInventoryUpdateEventProperties(_trainCarInstanceId, slot, itemStack));
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            SetItem(slot, ServerContext.ItemStackFactory.Create(itemId, count));
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            var currentItem = GetItem(slot);
            if (currentItem.Id == itemStack.Id)
            {
                // 同一アイテムは既存スタックへ加算して余りを返す
                // Add matching items to the existing stack and return the remainder.
                var result = currentItem.AddItem(itemStack);
                SetItem(slot, result.ProcessResultItemStack);
                return result.RemainderItemStack;
            }

            // 別アイテムはスロットを入れ替えて元アイテムを返す
            // Swap different items and return the previous slot item.
            SetItem(slot, itemStack);
            return currentItem;
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            return ReplaceItem(slot, ServerContext.ItemStackFactory.Create(itemId, count));
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            var currentItemStack = InsertToContainer(itemStack, false);
            return InsertToContainer(currentItemStack, true);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            var remains = new List<IItemStack>();
            foreach (var itemStack in itemStacks)
            {
                var remain = InsertItem(itemStack);
                if (remain.Id != ItemMaster.EmptyItemId) remains.Add(remain);
            }

            return remains;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            var inventoryCopy = InventoryItems.ToArray();
            foreach (var itemStack in itemStacks)
            {
                // コピー上で挿入結果を検証し、実コンテナは変更しない
                // Validate insertion on a copy without mutating the real container.
                var remain = InsertToArray(inventoryCopy, itemStack);
                if (remain.Id != ItemMaster.EmptyItemId) return false;
            }

            return true;

            #region Internal

            IItemStack InsertToArray(IItemStack[] inventory, IItemStack itemStack)
            {
                var currentItemStack = InsertToArraySlots(inventory, itemStack, false);
                return InsertToArraySlots(inventory, currentItemStack, true);
            }

            IItemStack InsertToArraySlots(IItemStack[] inventory, IItemStack itemStack, bool emptySlotOnly)
            {
                var currentItemStack = itemStack;
                for (var i = 0; i < inventory.Length; i++)
                {
                    if (currentItemStack.Id == ItemMaster.EmptyItemId) return currentItemStack;
                    if (!CanInsertToSlot(inventory[i], currentItemStack, emptySlotOnly)) continue;

                    var result = inventory[i].AddItem(currentItemStack);
                    inventory[i] = result.ProcessResultItemStack;
                    currentItemStack = result.RemainderItemStack;
                }

                return currentItemStack;
            }

            #endregion
        }

        public int GetSlotSize()
        {
            return _container.InventoryItems.Length;
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return new ReadOnlyCollection<IItemStack>(InventoryItems.ToList());
        }

        public override int GetHashCode()
        {
            return _trainCarInstanceId.GetHashCode();
        }

        private IItemStack InsertToContainer(IItemStack itemStack, bool emptySlotOnly)
        {
            var currentItemStack = itemStack;
            for (var i = 0; i < _container.InventoryItems.Length; i++)
            {
                if (currentItemStack.Id == ItemMaster.EmptyItemId) return currentItemStack;
                if (!CanInsertToSlot(GetItem(i), currentItemStack, emptySlotOnly)) continue;

                // スロットに入る分だけ加算して余りを次スロットへ回す
                // Add what fits in this slot and carry the remainder forward.
                var result = GetItem(i).AddItem(currentItemStack);
                SetItem(i, result.ProcessResultItemStack);
                currentItemStack = result.RemainderItemStack;
            }

            return currentItemStack;
        }

        private static bool CanInsertToSlot(IItemStack slotItem, IItemStack insertItem, bool emptySlotOnly)
        {
            if (emptySlotOnly) return slotItem.Id == ItemMaster.EmptyItemId;
            return slotItem.Id != ItemMaster.EmptyItemId && slotItem.Id == insertItem.Id;
        }
    }
}
