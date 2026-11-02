using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory.Input
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
        private readonly MachineInputSlotBinding _slotBinding;

        // ブロック搬入の口。自動搬入上限と、継承元のレベルで断った記録を持つ
        // The block-inflow inlet holding the auto-insert cap and the source-level refusal record
        private readonly MachineInputBlockInflow _blockInflow;

        public VanillaMachineInputInventory(
            BlockId blockId,
            int inputSlot,
            int innerTankCount,
            float innerTankCapacity,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate,
            BlockInstanceId blockInstanceId,
            IGameUnlockStateData gameUnlockStateData,
            ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            _blockId = blockId;
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _gameUnlockStateData = gameUnlockStateData;
            _slotBinding = new MachineInputSlotBinding(new MachineInputCraftCapGate(craftCapLookup));

            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, inputSlot, option);

            _fluidContainers = new FluidContainer[innerTankCount];
            for (var i = 0; i < innerTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }
            _blockInflow = new MachineInputBlockInflow(_itemDataStoreService, _slotBinding);
        }

        public BlockId BlockId => _blockId;

        // 直前に警告した (レシピ, スロット, 理由)。同じ組の警告を毎tick出さないための記録
        // The last warned (recipe, slot, reason); avoids repeating the same warning every tick
        private (Guid recipe, int slot, MachineSlotPlacementCheck reason)? _lastNotReadyWarning;

        internal void SetBoundRecipe(MachineRecipeMasterElement recipe)
        {
            _lastNotReadyWarning = null;
            _slotBinding.SetRecipe(recipe);
            _blockInflow.ClearRefusal();
        }

        public MachineSlotPlacementCheck CheckPlacement(int localSlot, IItemStack itemStack)
        {
            return _slotBinding.CheckPlacement(localSlot, itemStack);
        }

        // 入力・出力スロットはアイテムの最大スタックだけで決まる
        // Input and output slots are bounded only by the item's max stack
        public int GetSlotStackLimit() => int.MaxValue;

        public bool TryGetReadyInput(MachineRecipeMasterElement recipe, out int? inheritedLevel)
        {
            inheritedLevel = null;
            // silent-ok: 不足中は通常の待機 / 入力欄と機械状態に表示される
            if (!recipe.RecipeConfirmation(_blockId, InputSlot, FluidInputSlot)) return false;
            // 返却残余も現在の製作上限で判定する
            // Check retained inputs against the current craft cap
            for (var slot = 0; slot < recipe.InputItems.Length; slot++)
            {
                var placement = _slotBinding.CheckPlacement(slot, InputSlot[slot]);
                if (placement == MachineSlotPlacementCheck.Allowed) continue;
                var warning = (recipe.MachineRecipeGuid, slot, placement);
                if (_lastNotReadyWarning != warning) UnityEngine.Debug.LogWarning($"Machine input is not ready: recipe={recipe.MachineRecipeGuid} slot={slot} reason={placement}");
                _lastNotReadyWarning = warning;
                return false;
            }
            _lastNotReadyWarning = null;
            if (MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape))
                inheritedLevel = shape.ResolveSourceLevel(InputSlot[shape.SourceInputIndex].Id);
            return true;
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

        // ブロック搬入は、自動搬入上限で積み継承元のレベルの拒否を覚える搬入口を通す
        // Inflow from blocks goes through the inlet that stacks under the auto-insert cap and remembers source-level refusals
        internal IItemStack InsertItemFromBlock(IItemStack itemStack) => _blockInflow.Insert(itemStack);
        internal bool InsertionCheckFromBlock(List<IItemStack> itemStacks) => _blockInflow.CanInsertAll(itemStacks);
        public bool TryGetLevelStall(out MachineInputLevelStall stall) => _blockInflow.TryGetStall(out stall);

        // 束縛タンクへ液体を挿入する。指定タンクは束縛時のみ受け、指定無しは必要量→余剰容量の2パスで束縛タンクを満たす（ADR 0042 R5）
        // Insert fluid into the bound tanks; a designated tank only when bound, undesignated inflow fills bound tanks in two passes: requirement first, spare capacity second (ADR 0042 R5)
        public FluidStack InsertFluid(FluidStack fluidStack, int designatedTankIndex, out bool changed)
        {
            return MachineInputFluidInsertUtil.InsertFluid(_fluidContainers, _slotBinding, fluidStack, designatedTankIndex, out changed);
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
