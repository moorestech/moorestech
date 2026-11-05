using Game.Block.Blocks.Machine.Inventory.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using UnityEngine;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineBlockInventoryComponent : IOpenableBlockInventoryComponent, ISortExcludedSlots
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        // 統合スロット順のサブインベントリ列
        // Sub-inventories in unified slot order
        private readonly IVanillaMachineSubInventory[] _subInventories;

        public VanillaMachineBlockInventoryComponent(VanillaMachineInputInventory vanillaMachineInputInventory, VanillaMachineOutputInventory vanillaMachineOutputInventory, VanillaMachineModuleInventory vanillaMachineModuleInventory)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _subInventories = new IVanillaMachineSubInventory[] { vanillaMachineInputInventory, vanillaMachineOutputInventory, vanillaMachineModuleInventory };
        }

        public IReadOnlyList<IItemStack> InventoryItems
        {
            get
            {
                BlockException.CheckDestroy(this);
                var items = new List<IItemStack>();
                foreach (var subInventory in _subInventories) items.AddRange(subInventory.Items);
                return items;
            }
        }

        // スロットは全て束縛済みで整理対象にならない（ADR 0042）
        // Every slot is recipe-bound, so none participates in sorting (ADR 0042)
        public IReadOnlyCollection<int> SortExcludedSlots
        {
            get
            {
                BlockException.CheckDestroy(this);
                return Enumerable.Range(0, GetSlotSize()).ToList();
            }
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);

            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            return ReplaceItem(slot, item);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            return _vanillaMachineInputInventory.InsertItem(itemStack);
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);
            return _vanillaMachineInputInventory.InsertItemFromBlock(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            return subInventory.Items[localSlot];
        }

        // SetItemは言われたとおりに書き込む。束縛の可否判定は呼び出し側がIsAllowedToPlaceで事前に問い合わせる
        // SetItem always writes as instructed; callers query IsAllowedToPlace beforehand
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            subInventory.SetItem(localSlot, itemStack);
        }

        // 移動/挿入サービスが書き込み前に問い合わせる配置可否。CheckPlacementは拒否ログ用に理由まで返す
        // Placement check the move/insert services ask before writing; CheckPlacement also returns the reason for rejection logs
        public bool IsAllowedToPlace(int slot, IItemStack itemStack) => CheckPlacement(slot, itemStack) == MachineSlotPlacementCheck.Allowed;

        public MachineSlotPlacementCheck CheckPlacement(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);
            return subInventory.CheckPlacement(localSlot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            BlockException.CheckDestroy(this);

            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            SetItem(slot, item);
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);

            return _subInventories.Sum(subInventory => subInventory.Items.Count);
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            BlockException.CheckDestroy(this);

            return new ReadOnlyCollection<IItemStack>(InventoryItems.ToList());
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);

            return _vanillaMachineInputInventory.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);

            return _vanillaMachineInputInventory.InsertionCheck(itemStacks);
        }

        // ベルトの振り分け判定は自動搬入と同じ上限を参照する
        // Belt routing checks the same cap as automated insertion
        bool IBlockInventory.InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _vanillaMachineInputInventory.InsertionCheckFromBlock(itemStacks);
        }

        /// <summary>
        ///     アイテムの置き換え。同じアイテムIDか空きならスロット上限まで足して余りを返し、違うIDなら入れ替える
        /// </summary>
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            var (subInventory, localSlot) = ResolveSlot(slot);

            // 束縛外のスロットへは置けず、そのまま返す。移動サービスは事前照会で弾くため、ここへ来るのはそれ以外の呼び出し元
            // A stack that violates the binding bounces back untouched; the move service pre-checks, so only other callers reach here
            var placement = subInventory.CheckPlacement(localSlot, itemStack);
            if (placement != MachineSlotPlacementCheck.Allowed)
            {
                Debug.LogWarning($"[VanillaMachineBlockInventoryComponent] ReplaceItem rejected: reason={placement} sub={subInventory.GetType().Name}[{localSlot}] slot={slot} itemId={itemStack.Id} count={itemStack.Count}");
                return itemStack;
            }

            return MachineSubInventoryReplaceUtil.Replace(subInventory, localSlot, itemStack);
        }

        // スロット番号をサブインベントリとローカル番号へ解決（実装はVanillaMachineSlotResolutionUtilへ委譲）
        // Resolve a slot number to its sub-inventory and local index (delegated to VanillaMachineSlotResolutionUtil)
        private (IVanillaMachineSubInventory subInventory, int localSlot) ResolveSlot(int slot)
        {
            return VanillaMachineSlotResolutionUtil.ResolveSlot(_subInventories, slot);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
