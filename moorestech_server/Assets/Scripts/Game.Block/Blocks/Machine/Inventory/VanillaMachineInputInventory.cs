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
            var (slots, touchedSlots, remainders) = VanillaMachineInputSimulationUtil.SimulateInsert(InputSlot, itemStacks, _slotBinding);
            foreach (var slot in touchedSlots) _itemDataStoreService.SetItem(slot, slots[slot]);
            return remainders;
        }

        // 実挿入と同じ束縛規則で仮想挿入した際の残余を返す（書き込みは行わない）。返却シミュレーション等が使う唯一の口
        // Return the remainders of a virtual insert under the same binding rule as the real insert (no write). The single entry point for refund simulation etc.
        public List<IItemStack> SimulateInsert(IReadOnlyList<IItemStack> itemStacks)
        {
            return VanillaMachineInputSimulationUtil.SimulateInsert(InputSlot, itemStacks, _slotBinding).remainders;
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            foreach (var remainder in SimulateInsert(itemStacks))
            {
                if (remainder.Count != 0) return false;
            }
            return true;
        }

        // 束縛タンクへ液体を挿入する。指定タンクは束縛時のみ受け、指定無しは必要量→余剰容量の2パスで束縛タンクを満たす（ADR 0042 R5）
        // Insert fluid into the bound tanks; a designated tank only when bound, undesignated inflow fills bound tanks in two passes: requirement first, spare capacity second (ADR 0042 R5)
        public FluidStack InsertFluid(FluidStack fluidStack, int designatedTankIndex, out bool changed)
        {
            // レシピ未選択の機械はどのタンクも受け入れない
            // An unselected machine accepts nothing in any tank
            if (!_slotBinding.IsBound())
            {
                changed = false;
                return fluidStack;
            }

            if (0 <= designatedTankIndex && designatedTankIndex < FluidInputSlot.Count)
            {
                return InsertIntoDesignatedTank(designatedTankIndex, out changed);
            }

            // 先頭タンクへ全量入れると、同一液体を複数タンクが要求するレシピで後続タンクが永久に空のままになる
            // Dumping everything into the first tank leaves later tanks empty forever when several tanks require the same fluid
            var remaining = fluidStack;
            remaining = FillBoundTanks(remaining, true);
            remaining = FillBoundTanks(remaining, false);

            changed = remaining.Amount < fluidStack.Amount;
            return remaining;

            #region Internal

            FluidStack InsertIntoDesignatedTank(int designated, out bool designatedChanged)
            {
                if (!_slotBinding.IsFluidAllowedAt(designated, fluidStack.FluidId))
                {
                    designatedChanged = false;
                    return fluidStack;
                }

                var result = FluidInputSlot[designated].AddLiquid(fluidStack);
                designatedChanged = 0 < result.AcceptedAmount;
                return result.Remainder;
            }

            // limitToRequirementがtrueならレシピ必要量まで、falseならタンク容量まで満たす
            // With limitToRequirement the fill stops at the recipe requirement; otherwise it runs to the tank capacity
            FluidStack FillBoundTanks(FluidStack incoming, bool limitToRequirement)
            {
                foreach (var tankIndex in _slotBinding.ResolveBoundTanks(incoming.FluidId))
                {
                    if (incoming.Amount <= 0) break;

                    var container = FluidInputSlot[tankIndex];
                    var acceptable = limitToRequirement
                        ? Math.Min(incoming.Amount, _slotBinding.RequiredFluidAmountAt(tankIndex) - container.Amount)
                        : incoming.Amount;
                    if (acceptable <= 0) continue;

                    var result = container.AddLiquid(new FluidStack(acceptable, incoming.FluidId));
                    incoming = new FluidStack(incoming.Amount - result.AcceptedAmount, incoming.FluidId);
                }
                return incoming;
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

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot, itemStack));
        }
    }
}
