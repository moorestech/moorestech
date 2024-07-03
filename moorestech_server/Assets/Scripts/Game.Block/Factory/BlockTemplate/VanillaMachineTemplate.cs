using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Context;
using Game.EnergySystem;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
        public IBlock New(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var (input, output) = GetDependencies(config, blockInstanceId, inputConnectorComponent, _blockInventoryUpdateEvent);
            var machineParam = (MachineBlockConfigParam)config.Param;
            
            var emptyRecipe = ServerContext.MachineRecipeConfig.GetEmptyRecipeData();
            var processor = new VanillaMachineProcessorComponent(input, output, emptyRecipe, machineParam.RequiredPower);
            
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output);
            var machineSave = new VanillaMachineSaveComponent(input, output, processor);
            var machineComponent = new VanillaElectricMachineComponent(blockInstanceId, processor);
            
            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var (input, output) = GetDependencies(config, blockInstanceId, inputConnectorComponent, _blockInventoryUpdateEvent);
            var machineParam = (MachineBlockConfigParam)config.Param;
            
            var processor = LoadState(state, input, output, machineParam.RequiredPower);
            
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output);
            var machineSave = new VanillaMachineSaveComponent(input, output, processor);
            var machineComponent = new VanillaElectricMachineComponent(blockInstanceId, processor);
            
            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                machineComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
        
        public static (VanillaMachineInputInventory, VanillaMachineOutputInventory) GetDependencies(
            BlockConfigData param,
            BlockInstanceId blockInstanceId,
            BlockConnectorComponent<IBlockInventory> blockConnectorComponent,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            var machineParam = param.Param as IMachineBlockParam;
            
            var input = new VanillaMachineInputInventory(
                param.BlockId, machineParam.InputSlot,
                blockInventoryUpdateEvent, blockInstanceId);
            
            var output = new VanillaMachineOutputInventory(
                machineParam.OutputSlot, ServerContext.ItemStackFactory, blockInventoryUpdateEvent, blockInstanceId,
                machineParam.InputSlot, blockConnectorComponent);
            
            return (input, output);
        }
        
        public static VanillaMachineProcessorComponent LoadState(
            string state,
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ElectricPower requestPower)
        {
            var jsonObject = JsonConvert.DeserializeObject<VanillaMachineJsonObject>(state);
            
            var inputItems = jsonObject.InputSlot.Select(item => item.ToItem()).ToList();
            for (var i = 0; i < inputItems.Count; i++)
            {
                vanillaMachineInputInventory.SetItem(i, inputItems[i]);
            }
            
            var outputItems = jsonObject.OutputSlot.Select(item => item.ToItem()).ToList();
            for (var i = 0; i < outputItems.Count; i++)
            {
                vanillaMachineOutputInventory.SetItem(i, outputItems[i]);
            }
            
            var recipe = ServerContext.MachineRecipeConfig.GetRecipeData(jsonObject.RecipeId);
            
            var processor = new VanillaMachineProcessorComponent(
                vanillaMachineInputInventory,
                vanillaMachineOutputInventory,
                (ProcessState)jsonObject.State,
                jsonObject.RemainingTime,
                recipe,
                requestPower);
            
            return processor;
        }
    }
}