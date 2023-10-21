using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Game.Block.Event;
using Game.Block.Interface.Event;
using Game.Block.Interface.RecipeConfig;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     
    ///     InsertInput
    /// </summary>
    public class VanillaMachineInputInventory
    {
        private readonly int _blockId;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly int _entityId;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly IMachineRecipeConfig _machineRecipeConfig;

        public VanillaMachineInputInventory(int blockId, int inputSlot, IMachineRecipeConfig machineRecipeConfig,
            ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent blockInventoryUpdate, int entityId)
        {
            _blockId = blockId;
            _machineRecipeConfig = machineRecipeConfig;
            _blockInventoryUpdate = blockInventoryUpdate;
            _entityId = entityId;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, inputSlot);
        }

        public IReadOnlyList<IItemStack> InputSlot => _itemDataStoreService.Inventory;

        public bool IsAllowedToStartProcess
        {
            get
            {
                //ID
                var recipe = _machineRecipeConfig.GetRecipeData(_blockId, InputSlot);
                
                return recipe.RecipeConfirmation(InputSlot, _blockId);
            }
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _itemDataStoreService.InsertItem(itemStack);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _itemDataStoreService.InsertItem(itemStacks);
        }

        public MachineRecipeData GetRecipeData()
        {
            return _machineRecipeConfig.GetRecipeData(_blockId, InputSlot);
        }

        public void ReduceInputSlot(MachineRecipeData recipe)
        {
            //input
            foreach (var item in recipe.ItemInputs)
                for (var i = 0; i < InputSlot.Count; i++)
                {
                    if (_itemDataStoreService.Inventory[i].Id != item.Id || item.Count > InputSlot[i].Count) continue;
                    
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
                _entityId, slot, itemStack));
        }
    }
}