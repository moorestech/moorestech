using System.Collections.Generic;
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
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineOutputInventory
    {
        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.InventoryItems;
        public IReadOnlyList<FluidContainer> FluidOutputSlot => _fluidContainers;
        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly BlockInstanceId _blockInstanceId;
        
        private readonly int _inputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer[] _fluidContainers;
        
        public VanillaMachineOutputInventory(int outputSlot, int outputTankCount, float innerTankCapacity, IItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId, int inputSlotSize, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _inputSlotSize = inputSlotSize;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlot);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockConnectorComponent);
            
            _fluidContainers = new FluidContainer[outputTankCount];
            for (var i = 0; i < outputTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }
            
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
            
            // 液体の出力スペースをチェック
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) return false;
                
                var outputFluid = machineRecipe.OutputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(outputFluid.FluidGuid);
                
                // 既に異なる液体が入っている場合、または容量が不足している場合
                if (_fluidContainers[i].FluidId != FluidMaster.EmptyFluidId && _fluidContainers[i].FluidId != fluidId)
                {
                    return false;
                }
                
                if (_fluidContainers[i].Capacity - _fluidContainers[i].Amount < outputFluid.Amount)
                {
                    return false;
                }
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
            
            //アウトプットスロットに液体を格納する
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) break;
                
                var outputFluid = machineRecipe.OutputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(outputFluid.FluidGuid);
                var fluidStack = new FluidStack(outputFluid.Amount, fluidId);
                
                _fluidContainers[i].AddLiquid(fluidStack, FluidContainer.Empty);
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

        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItemWithoutEvent(slot, itemStack);
        }


        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot + _inputSlotSize, itemStack));
        }
    }
}