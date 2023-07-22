using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Service;
using Core.Block.Event;
using Core.Block.RecipeConfig.Data;
using Core.Inventory;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineOutputInventory : IUpdatable
    {
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly int _entityId;

        private readonly int _inputSlotSize;


        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.Inventory;

        public VanillaMachineOutputInventory(int outputSlot, ItemStackFactory itemStackFactory, 
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, int entityId, int inputSlotSize)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _entityId = entityId;
            _inputSlotSize = inputSlotSize;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent,itemStackFactory,outputSlot);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(_connectInventory);
            GameUpdater.RegisterUpdater(this);
        }

        /// <summary>
        /// アウトプットスロットにアイテムを入れれるかチェック
        /// </summary>
        /// <param name="machineRecipeData"></param>
        /// <returns>スロットに空きがあったらtrue</returns>
        public bool IsAllowedToOutputItem(IMachineRecipeData machineRecipeData)
        {
            foreach (var itemOutput in machineRecipeData.ItemOutputs)
            {
                var isAllowed = OutputSlot.Aggregate(false,
                    (current, slot) => slot.IsAllowedToAdd(itemOutput.OutputItem) || current);

                if (!isAllowed) return false;
            }

            return true;
        }

        public void InsertOutputSlot(IMachineRecipeData machineRecipeData)
        {
            //アウトプットスロットにアイテムを格納する
            foreach (var output in machineRecipeData.ItemOutputs)
            {
                for (int i = 0; i < OutputSlot.Count; i++)
                {
                    if (!OutputSlot[i].IsAllowedToAdd(output.OutputItem)) continue;

                    var item = OutputSlot[i].AddItem(output.OutputItem).ProcessResultItemStack;
                    _itemDataStoreService.SetItem(i,item);
                    break;
                }
            }
        }

        void InsertConnectInventory()
        {
            for (int i = 0; i < OutputSlot.Count; i++)
            {
                _itemDataStoreService.SetItem(i,_connectInventoryService.InsertItem(OutputSlot[i]));
            }
        }

        public void AddConnectInventory(IBlockInventory blockInventory)
        {
            _connectInventory.Add(blockInventory);
            //NullInventoryは削除しておく
            for (int i = _connectInventory.Count - 1; i >= 0; i--)
            {
                if (_connectInventory[i] is NullIBlockInventory)
                {
                    _connectInventory.RemoveAt(i);
                }
            }
        }

        public void RemoveConnectInventory(IBlockInventory blockInventory) { _connectInventory.Remove(blockInventory); }

        public void Update() { InsertConnectInventory(); }

        public void SetItem(int slot, IItemStack itemStack) { _itemDataStoreService.SetItem(slot,itemStack); }
        
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _entityId,slot + _inputSlotSize,itemStack));
        }
    }
}