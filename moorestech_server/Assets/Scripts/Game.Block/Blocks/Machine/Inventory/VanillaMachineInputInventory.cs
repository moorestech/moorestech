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
        private readonly BlockId _blockId;
        private readonly BlockInstanceId _blockInstanceId;
        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        //TODO: ↑のようにする
        private readonly FluidContainer[] _fluidContainers;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        public VanillaMachineInputInventory(BlockId blockId, int inputSlot, int fluidContainerCount, float fluidContainerCapacity, BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId)
        {
            _blockId = blockId;
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _fluidContainers = new FluidContainer[fluidContainerCount];
            for (var i = 0; i < fluidContainerCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(fluidContainerCapacity);
            }
            
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, inputSlot);
        }
        
        public IReadOnlyList<IItemStack> InputSlot => _itemDataStoreService.InventoryItems;
        public IReadOnlyList<FluidContainer> FluidInputSlot => _fluidContainers;
        
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
                for (var i = 0; i < InputSlot.Count; i++)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(item.ItemGuid);
                    
                    if (_itemDataStoreService.InventoryItems[i].Id != itemId || item.Count > InputSlot[i].Count) continue;
                    //アイテムを減らす
                    _itemDataStoreService.SetItem(i, InputSlot[i].SubItem(item.Count));
                    break;
                }
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
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