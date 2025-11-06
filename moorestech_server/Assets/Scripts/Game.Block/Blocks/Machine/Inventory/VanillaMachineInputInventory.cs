using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     インプットのインベントリとアウトプットのインベントリを同じように扱う
    ///     Insertなどの処理は基本的にInputのインベントリにのみ行う
    /// </summary>
    public class VanillaMachineInputInventory
    {
        public IReadOnlyList<IItemStack> InputSlot => _itemDataStoreService.InventoryItems;
        public IReadOnlyList<FluidContainer> FluidInputSlot => _fluidContainers;
        
        private readonly BlockId _blockId;
        private readonly BlockInstanceId _blockInstanceId;
        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly FluidContainer[] _fluidContainers;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        public VanillaMachineInputInventory(BlockId blockId, int inputSlot, int innerTankCount, float innerTankCapacity, BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId)
        {
            _blockId = blockId;
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            
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
        
        public bool IsAllowedToStartProcess()
        {
            //ブロックIDと現在のインプットスロットからレシピを検索する
            if (TryGetRecipeElement(out var recipe))
            {
                //実行できるレシピかどうか
                return recipe.RecipeConfirmation(_blockId, InputSlot, FluidInputSlot);
            }
            return false;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _itemDataStoreService.InsertItem(itemStack);
        }
        
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _itemDataStoreService.InsertItem(itemStacks);
        }
        
        public bool TryGetRecipeElement(out MachineRecipeMasterElement recipe)
        {
            return MachineRecipeMasterUtil.TryGetRecipeElement(_blockId, InputSlot, FluidInputSlot, out recipe);
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
            // ブロックがWorldBlockDatastoreに登録されていない場合はイベントを発火しない
            // Do not fire events if the block is not registered in WorldBlockDatastore
            if (ServerContext.WorldBlockDatastore.GetBlock(_blockInstanceId) == null)
            {
                return;
            }

            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot, itemStack));
        }
    }
}
