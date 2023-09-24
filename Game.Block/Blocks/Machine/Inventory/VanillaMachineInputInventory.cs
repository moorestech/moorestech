using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Block.Event;
using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;

namespace Core.Block.Blocks.Machine.Inventory
{
    /// <summary>
    /// インプットのインベントリとアウトプットのインベントリを同じように扱う
    /// Insertなどの処理は基本的にInputのインベントリにのみ行う
    /// </summary>
    public class VanillaMachineInputInventory
    {
        private readonly int _blockId;
        private readonly int _entityId;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;

        public IReadOnlyList<IItemStack> InputSlot => _itemDataStoreService.Inventory;

        public VanillaMachineInputInventory(int blockId, int inputSlot, IMachineRecipeConfig machineRecipeConfig,
            ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent blockInventoryUpdate, int entityId)
        {
            _blockId = blockId;
            _machineRecipeConfig = machineRecipeConfig;
            _blockInventoryUpdate = blockInventoryUpdate;
            _entityId = entityId;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory, inputSlot);
        }

        public IItemStack InsertItem(IItemStack itemStack) { return _itemDataStoreService.InsertItem(itemStack); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { return _itemDataStoreService.InsertItem(itemStacks); }

        public bool IsAllowedToStartProcess
        {
            get
            {
                //建物IDと現在のインプットスロットからレシピを検索する
                var recipe = _machineRecipeConfig.GetRecipeData(_blockId, InputSlot);
                //実行できるレシピかどうか
                return recipe.RecipeConfirmation(InputSlot);
            }
        }

        public MachineRecipeData GetRecipeData() { return _machineRecipeConfig.GetRecipeData(_blockId, InputSlot); }

        public void ReduceInputSlot(MachineRecipeData recipe)
        {
            //inputスロットからアイテムを減らす
            foreach (var item in recipe.ItemInputs)
            {
                for (var i = 0; i < InputSlot.Count; i++)
                {
                    if (_itemDataStoreService.Inventory[i].Id != item.Id || item.Count > InputSlot[i].Count) continue;
                    //アイテムを減らす
                    _itemDataStoreService.SetItem(i,InputSlot[i].SubItem(item.Count));
                    break;
                }
            }
        }

        public void SetItem(int slot, IItemStack itemStack) { _itemDataStoreService.SetItem(slot,itemStack); }
        public bool InsertionCheck(List<IItemStack> itemStacks) { return _itemDataStoreService.InsertionCheck(itemStacks); }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _entityId,slot,itemStack));
        }
    }
}