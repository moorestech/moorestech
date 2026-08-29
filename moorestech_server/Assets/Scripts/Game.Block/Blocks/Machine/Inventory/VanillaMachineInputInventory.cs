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

        public void SetBoundRecipe(MachineRecipeMasterElement recipe)
        {
            _slotBinding.SetRecipe(recipe);
        }

        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            return _slotBinding.IsAllowedToPlace(localSlot, itemStack);
        }

        public bool IsFluidAllowedAt(int tankIndex, FluidId fluidId)
        {
            return _slotBinding.IsFluidAllowedAt(tankIndex, fluidId);
        }

        // このアイテムが積まれるスロット番号を公開する（返却シミュレーション等、束縛規則を外部から再現する用途）
        // Expose the bound slot for this item (used by callers, e.g. refund simulation, that must mirror the binding rule)
        public int ResolveSlot(IItemStack itemStack)
        {
            return _slotBinding.ResolveSlot(itemStack);
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
            // 素材iはスロットiにだけ積む。レシピ外・未選択はそのまま返す
            // Input i stacks only into slot i; foreign items and unselected machines bounce it back
            var slot = _slotBinding.ResolveSlot(itemStack);
            if (slot < 0) return itemStack;
            var result = InputSlot[slot].AddItem(itemStack);
            _itemDataStoreService.SetItem(slot, result.ProcessResultItemStack);
            return result.RemainderItemStack;
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            var remainders = new List<IItemStack>(itemStacks.Count);
            foreach (var stack in itemStacks) remainders.Add(InsertItem(stack));
            return remainders;
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
                if (container.Amount > 0) continue;
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

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            // 実挿入と同じ束縛規則でスロット複製へ仮想挿入する
            // Virtually insert into copied slots under the same binding rule as the real insert
            var simulated = new List<IItemStack>(InputSlot);
            foreach (var stack in itemStacks)
            {
                var slot = _slotBinding.ResolveSlot(stack);
                if (slot < 0) return false;
                var result = simulated[slot].AddItem(stack);
                if (result.RemainderItemStack.Count != 0) return false;
                simulated[slot] = result.ProcessResultItemStack;
            }
            return true;
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot, itemStack));
        }
    }
}
