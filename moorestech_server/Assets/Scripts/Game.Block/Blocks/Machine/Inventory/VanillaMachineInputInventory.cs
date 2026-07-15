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
            
            var option = new OpenableInventoryItemDataStoreServiceOption()
            {
                AllowMultipleStacksPerItemOnInsert = false,
            };
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, inputSlot, option);
            
            _fluidContainers = new FluidContainer[innerTankCount];
            for (var i = 0; i < innerTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }
        }
        
        public BlockId BlockId => _blockId;

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
            return _itemDataStoreService.InsertItem(itemStack);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _itemDataStoreService.InsertItem(itemStacks);
        }
        
        public void ReduceInputSlot(MachineRecipeMasterElement recipe)
        {
            //inputスロットからアイテムを減らす
            foreach (var item in recipe.InputItems)
            {
                if (item.IsRemain.HasValue && item.IsRemain.Value) continue;
                
                for (var i = 0; i < InputSlot.Count; i++)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(item.ItemGuid);
                    
                    if (_itemDataStoreService.InventoryItems[i].Id != itemId || item.Count > InputSlot[i].Count) continue;
                    //アイテムを減らす
                    _itemDataStoreService.SetItem(i, InputSlot[i].SubItem(item.Count));
                    break;
                }
            }
            
            //inputスロットから液体を減らす
            foreach (var inputFluid in recipe.InputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                
                // 任意のスロットから必要な液体を減らす
                for (var i = 0; i < _fluidContainers.Length; i++)
                {
                    if (_fluidContainers[i].FluidId == fluidId && _fluidContainers[i].Amount >= inputFluid.Amount)
                    {
                        _fluidContainers[i].Amount -= inputFluid.Amount;
                        
                        // If the container is now empty, reset the fluid ID
                        if (_fluidContainers[i].Amount <= 0)
                        {
                            _fluidContainers[i].Amount = 0;
                            _fluidContainers[i].FluidId = FluidMaster.EmptyFluidId;
                        }
                        break; // 一つのスロットから減らしたら次の液体へ
                    }
                }
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
            return _itemDataStoreService.InsertionCheck(itemStacks);
        }
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot, itemStack));
        }
    }
}
