using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     インプットのインベントリとアウトプットのインベントリを同じように扱う
    ///     Insertなどの処理は基本的にInputのインベントリにのみ行う
    /// </summary>
    public class VanillaMachineInputInventory : IVanillaMachineSubInventory
    {
        public IReadOnlyList<IItemStack> InputSlot => _itemDataStoreService.InventoryItems;
        IReadOnlyList<IItemStack> IVanillaMachineSubInventory.Items => InputSlot;
        public IReadOnlyList<FluidContainer> FluidInputSlot => _fluidContainers;

        private readonly BlockId _blockId;
        private readonly BlockInstanceId _blockInstanceId;

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly FluidContainer[] _fluidContainers;
        private readonly IGameUnlockStateData _gameUnlockStateData;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        // 選択レシピへのスロット束縛判定。未選択は何も受け入れない（ADR 0042）
        // Slot-binding decision against the selected recipe; unselected accepts nothing (ADR 0042)
        private readonly MachineInputSlotBinding _slotBinding = new();

        public VanillaMachineInputInventory(
            BlockId blockId,
            int inputSlot,
            int innerTankCount,
            float innerTankCapacity,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate,
            BlockInstanceId blockInstanceId,
            IGameUnlockStateData gameUnlockStateData)
        {
            _blockId = blockId;
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _gameUnlockStateData = gameUnlockStateData;

            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, inputSlot, option);
            _slotBinding.SetSlotCount(inputSlot);

            _fluidContainers = new FluidContainer[innerTankCount];
            for (var i = 0; i < innerTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }
        }

        public BlockId BlockId => _blockId;

        internal void SetBoundRecipe(MachineRecipeMasterElement recipe)
        {
            _slotBinding.SetRecipe(recipe);
        }

        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            return _slotBinding.IsAllowedToPlace(localSlot, itemStack);
        }

        public bool IsAllowedToStartProcess(MachineRecipeMasterElement recipe)
        {
            // 選択済みレシピの材料充足のみを確認する（レシピ探索は行わない）
            // Only verify the selected recipe's inputs are satisfied (no recipe search)
            return recipe.RecipeConfirmation(_blockId, InputSlot, FluidInputSlot);
        }

        public bool IsRecipeUnlocked(Guid machineRecipeGuid)
        {
            return _gameUnlockStateData.MachineRecipeUnlockStateInfos.TryGetValue(machineRecipeGuid, out var info) && info.IsUnlocked;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return InsertItem(new List<IItemStack> { itemStack })[0];
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            var (slots, touchedSlots, remainders) = SimulateInsertCore(itemStacks);
            foreach (var slot in touchedSlots) _itemDataStoreService.SetItem(slot, slots[slot]);
            return remainders;
        }

        // 実挿入と同じ束縛規則で仮想挿入した際の残余を返す（書き込みは行わない）。返却シミュレーション等が使う唯一の口
        // Return the remainders of a virtual insert under the same binding rule as the real insert (no write). The single entry point for refund simulation etc.
        public List<IItemStack> SimulateInsert(IReadOnlyList<IItemStack> itemStacks)
        {
            return SimulateInsertCore(itemStacks).remainders;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            foreach (var remainder in SimulateInsert(itemStacks))
            {
                if (remainder.Count != 0) return false;
            }
            return true;
        }

        // 束縛タンクへ液体を挿入する。指定タンクは束縛時のみ受け、指定無しは束縛タンクへ直行する（ADR 0042 R5）
        // Insert fluid into the bound tank; a designated tank only when bound, undesignated inflow goes straight to the bound tank (ADR 0042 R5)
        public FluidStack InsertFluid(FluidStack fluidStack, int designatedTankIndex, out bool changed)
        {
            var index = ResolveFluidTargetIndex(designatedTankIndex, fluidStack.FluidId);
            if (index < 0)
            {
                changed = false;
                return fluidStack;
            }

            var result = FluidInputSlot[index].AddLiquid(fluidStack);
            changed = 0 < result.AcceptedAmount;
            return result.Remainder;

            #region Internal

            // タンク指定ありは束縛の合否のみ、指定無しは束縛タンクを先頭から探索する
            // A designated tank is judged solely on the binding; undesignated inflow scans for the bound tank from the front
            int ResolveFluidTargetIndex(int designated, FluidId fluidId)
            {
                if (0 <= designated && designated < FluidInputSlot.Count)
                {
                    return IsFluidAllowedAt(designated, fluidId) ? designated : -1;
                }

                for (var i = 0; i < FluidInputSlot.Count; i++)
                {
                    if (IsFluidAllowedAt(i, fluidId)) return i;
                }
                return -1;
            }

            #endregion
        }

        public void ReduceInputSlot(MachineRecipeMasterElement recipe)
        {
            // 素材iはスロットiから減らす（束縛済みなので探索しない）
            // Consume input i from slot i (bound, so no search)
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                var item = recipe.InputItems[i];
                if (item.IsRemain.HasValue && item.IsRemain.Value) continue;
                _itemDataStoreService.SetItem(i, InputSlot[i].SubItem(item.Count));
            }

            // 液体iはタンクiから減らす（束縛済みなので探索しない）
            // Consume fluid i from tank i (bound, so no search)
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var container = _fluidContainers[i];
                container.Amount -= recipe.InputFluids[i].Amount;
                if (0 < container.Amount) continue;
                container.Amount = 0;
                container.FluidId = FluidMaster.EmptyFluidId;
            }
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }

        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItemWithoutEvent(slot, itemStack);
        }

        // 束縛規則で全itemStacksを仮想挿入し、変更後スロット値・触れたスロット・各要求ごとの残余を返す（副作用なし）
        // Virtually insert every itemStack under the binding rule and return the post-insert slots, touched slots, and per-request remainders (no side effects)
        private (List<IItemStack> slots, List<int> touchedSlots, List<IItemStack> remainders) SimulateInsertCore(IReadOnlyList<IItemStack> itemStacks)
        {
            var slots = new List<IItemStack>(InputSlot);
            var touchedSlots = new List<int>();
            var remainders = new List<IItemStack>(itemStacks.Count);

            foreach (var stack in itemStacks)
            {
                var remaining = stack;
                foreach (var slot in _slotBinding.ResolveBoundSlots(remaining.Id))
                {
                    if (remaining.Count == 0) break;
                    var result = slots[slot].AddItem(remaining);
                    slots[slot] = result.ProcessResultItemStack;
                    remaining = result.RemainderItemStack;
                    if (!touchedSlots.Contains(slot)) touchedSlots.Add(slot);
                }
                remainders.Add(remaining);
            }

            return (slots, touchedSlots, remainders);
        }

        private bool IsFluidAllowedAt(int tankIndex, FluidId fluidId)
        {
            return _slotBinding.IsFluidAllowedAt(tankIndex, fluidId);
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot, itemStack));
        }
    }
}
