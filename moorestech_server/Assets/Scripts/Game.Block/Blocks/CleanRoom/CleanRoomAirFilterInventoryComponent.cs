using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Interface.Component;
using Game.Context;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     清浄機のフィルタースロットをUIとベルトへ公開し、フィルター以外を受け付けない
    ///     Exposes the purifier's filter slot to the UI and belts, refusing anything but filters
    /// </summary>
    public class CleanRoomAirFilterInventoryComponent : IOpenableBlockInventoryComponent
    {
        private readonly OpenableInventoryItemDataStoreService _filterSlot;
        private readonly ItemId _filterItemId;

        public CleanRoomAirFilterInventoryComponent(OpenableInventoryItemDataStoreService filterSlot, ItemId filterItemId)
        {
            _filterSlot = filterSlot;
            _filterItemId = filterItemId;
        }

        public IReadOnlyList<IItemStack> InventoryItems => _filterSlot.InventoryItems;

        public IItemStack GetItem(int slot)
        {
            CheckDestroy(this);
            return _filterSlot.GetItem(slot);
        }

        // 手動移動はこの判定で拒否され、移動サービスが理由をログと通知に出す
        // Manual moves are refused here and the move service logs and notifies the reason
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            return IsFilterOrEmpty(itemStack);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            _filterSlot.SetItem(slot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            _filterSlot.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            return _filterSlot.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            return _filterSlot.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            CheckDestroy(this);

            // silent-ok: フィルター以外は受け取らず全量を差し戻す / 差し戻した品がプレイヤーのインベントリに残り、スロットに入らないのを見て気づく
            // silent-ok: non-filters are handed back whole / the returned item stays in the player inventory and the player notices it did not enter the slot
            if (!IsFilterOrEmpty(itemStack)) return itemStack;
            return _filterSlot.InsertItem(itemStack);
        }

        // 自動搬入は使用中を含めスロット表示で3個まで（ADR 0101）
        // Automated inflow stops at three in the slot, the one in use included (ADR 0101)
        private const int AutoInsertFilterLimit = MachineInputSlotBinding.AutoInsertCraftCount;

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            CheckDestroy(this);

            // silent-ok: フィルター以外は受け取らず全量を差し戻す / ベルトの出口に品が留まって詰まるのをプレイヤーが見て気づく
            // silent-ok: non-filters are handed back whole / the player sees the item parked and clogging at the belt exit
            if (!IsFilterOrEmpty(itemStack) || itemStack.Count == 0) return itemStack;

            // 上限分だけ渡し残りは返す
            // Pass up to the cap; return the rest
            var acceptable = Math.Min(itemStack.Count, AutoInsertAcceptableCount());
            // silent-ok: 3個に達したら搬入を止める / 品がベルト出口・チェストに留まり、プレイヤーがスロットの3個と並べて見て気づく
            // silent-ok: inflow stops at three / the item parks at the belt exit or chest and the player sees it next to the slot's three
            if (acceptable <= 0) return itemStack;

            // 前例（VanillaMachineInputSimulationUtil）と同じく SubItem で分け、品のメタデータを保つ
            // Split with SubItem like the precedent (VanillaMachineInputSimulationUtil) to keep item metadata
            var portion = acceptable == itemStack.Count ? itemStack : itemStack.SubItem(itemStack.Count - acceptable);
            var slotRemainder = _filterSlot.InsertItem(portion);
            return itemStack.SubItem(acceptable - slotRemainder.Count);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);

            // フィルターだけをスロットへ渡し、それ以外は残りとして返す
            // Hand only filters to the slot and return the rest as remainders
            var filters = new List<IItemStack>();
            var rejected = new List<IItemStack>();
            foreach (var itemStack in itemStacks)
            {
                if (IsFilterOrEmpty(itemStack)) filters.Add(itemStack);
                else rejected.Add(itemStack);
            }

            var remainders = _filterSlot.InsertItem(filters);
            remainders.AddRange(rejected);
            return remainders;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            foreach (var itemStack in itemStacks)
            {
                if (!IsFilterOrEmpty(itemStack)) return false;
            }

            return _filterSlot.InsertionCheck(itemStacks);
        }

        // 振り分け判定も同じ上限を参照
        // Belt routing checks the same cap as automated insertion
        bool IBlockInventory.InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            var total = 0;
            foreach (var itemStack in itemStacks)
            {
                if (!IsFilterOrEmpty(itemStack)) return false;
                total += itemStack.Count;
            }

            return total <= AutoInsertAcceptableCount() && _filterSlot.InsertionCheck(itemStacks);
        }

        public int GetSlotSize()
        {
            CheckDestroy(this);
            return _filterSlot.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            CheckDestroy(this);
            return _filterSlot.CreateCopiedItems();
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        private bool IsFilterOrEmpty(IItemStack itemStack)
        {
            return itemStack.Id == ItemMaster.EmptyItemId || itemStack.Id == _filterItemId;
        }

        // 上限までの残り受け入れ数。空スロットは0
        // How many more filters fit under the cap; an empty slot counts 0
        private int AutoInsertAcceptableCount()
        {
            return AutoInsertFilterLimit - _filterSlot.GetItem(0).Count;
        }
    }
}
