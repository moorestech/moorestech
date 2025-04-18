﻿using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineOutputInventory
    {
        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.InventoryItems;
        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly BlockInstanceId _blockInstanceId;
        
        private readonly int _inputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        
        public VanillaMachineOutputInventory(int outputSlot, IItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId, int inputSlotSize, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _inputSlotSize = inputSlotSize;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlot);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockConnectorComponent);
            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }
        
        private void Update()
        {
            InsertConnectInventory();
        }
        
        /// <summary>
        ///     アウトプットスロットにアイテムを入れれるかチェック
        /// </summary>
        /// <param name="machineRecipe"></param>
        /// <returns>スロットに空きがあったらtrue</returns>
        public bool IsAllowedToOutputItem(MachineRecipeMasterElement machineRecipe)
        {
            foreach (var itemOutput in machineRecipe.OutputItems)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                var outputItemStack = ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count);
                
                var isAllowed = OutputSlot.Aggregate(false, (current, slot) => slot.IsAllowedToAdd(outputItemStack) || current);
                
                if (!isAllowed) return false;
            }
            
            return true;
        }
        
        public void InsertOutputSlot(MachineRecipeMasterElement machineRecipe)
        {
            //アウトプットスロットにアイテムを格納する
            foreach (var itemOutput in machineRecipe.OutputItems)
                for (var i = 0; i < OutputSlot.Count; i++)
                {
                    var outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                    var outputItemStack = ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count);
                    
                    if (!OutputSlot[i].IsAllowedToAdd(outputItemStack)) continue;
                    
                    var item = OutputSlot[i].AddItem(outputItemStack).ProcessResultItemStack;
                    _itemDataStoreService.SetItem(i, item);
                    break;
                }
        }
        
        private void InsertConnectInventory()
        {
            for (var i = 0; i < OutputSlot.Count; i++)
                _itemDataStoreService.SetItem(i, _connectInventoryService.InsertItem(OutputSlot[i]));
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }
        
        
        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot + _inputSlotSize, itemStack));
        }
    }
}