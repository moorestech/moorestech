using System;
using Core.Block.BlockInventory;
using Core.Block.Blocks;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Machine.InventoryController;
using Core.Block.Blocks.Machine.SaveLoad;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        public delegate VanillaMachineBase CreateMachine(
            (int blockId, int entityId, ulong blockHash, VanillaMachineBlockInventory vanillaMachineBlockInventory, VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess, ItemStackFactory itemStackFactory) data);
        private readonly CreateMachine _createMachine;
        
        
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;


        public VanillaMachineTemplate(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory, 
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent,CreateMachine createMachine)
        {
            _machineRecipeConfig = machineRecipeConfig;
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
            _createMachine = createMachine;
        }

        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            var(input, output, machineParam) = GetData(param,entityId);

            var runProcess = new VanillaMachineRunProcess(input, output, _machineRecipeConfig.GetNullRecipeData(),
                machineParam.RequiredPower);

            return _createMachine((param.BlockId, entityId,blockHash,
                new VanillaMachineBlockInventory(input, output),
                new VanillaMachineSave(input, output, runProcess),
                runProcess,
                _itemStackFactory
            ));
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var(input, output, machineParam) = GetData(param,entityId);

            var runProcess = new VanillaMachineLoad(input, output, _itemStackFactory, _machineRecipeConfig,
                machineParam.RequiredPower).LoadVanillaMachineRunProcess(state);


            return _createMachine((param.BlockId, entityId,blockHash,
                new VanillaMachineBlockInventory(input, output),
                new VanillaMachineSave(input, output, runProcess),
                runProcess,
                _itemStackFactory
            ));
        }

        private (VanillaMachineInputInventory, VanillaMachineOutputInventory,MachineBlockConfigParam) GetData(BlockConfigData param,int entityId)
        {
            var machineParam = param.Param as MachineBlockConfigParam;
            
            var input = new VanillaMachineInputInventory(
                param.BlockId, machineParam.InputSlot, _machineRecipeConfig, _itemStackFactory, _blockInventoryUpdateEvent,entityId);

            var output = new VanillaMachineOutputInventory(
                machineParam.OutputSlot, _itemStackFactory, _blockInventoryUpdateEvent,entityId,machineParam.InputSlot);
            
            return (input, output, machineParam);
        }
    }
}