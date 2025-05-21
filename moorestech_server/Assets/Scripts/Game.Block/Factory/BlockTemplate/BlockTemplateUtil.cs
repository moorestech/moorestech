using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    public class BlockTemplateUtil
    {
        public static BlockConnectorComponent<IBlockInventory> CreateInventoryConnector(InventoryConnects inventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(inventoryConnects.InputConnects, inventoryConnects.OutputConnects, blockPositionInfo);
        }
        
        // TODO 保存ステートを誰でも持てるようになったので、このあたりも各自でセーブ、ロードできるように簡略化したい
        public static (VanillaMachineInputInventory, VanillaMachineOutputInventory) GetMachineIOInventory(
            BlockId blockId, BlockInstanceId blockInstanceId,
            IMachineParam machineParam,
            BlockConnectorComponent<IBlockInventory> blockConnectorComponent,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            var inputSlotCount = machineParam.InputSlotCount;
            var outputSlotCount = machineParam.OutputSlotCount;
            var fluidInputSlotCount = machineParam.InputFluidSlotCount;
            var fluidOutputSlotCount = machineParam.OutputFluidSlotCount;
            
            var input = new VanillaMachineInputInventory(
                blockId,
                inputSlotCount,
                machineParam.FluidContainerCount,
                machineParam.FluidContainerCapacity,
                blockInventoryUpdateEvent,
                blockInstanceId
            );
            
            var output = new VanillaMachineOutputInventory(
                outputSlotCount,
                fluidOutputSlotCount,
                machineParam.FluidContainerCapacity,
                ServerContext.ItemStackFactory,
                blockInventoryUpdateEvent,
                blockInstanceId,
                inputSlotCount,
                blockConnectorComponent);
            
            return (input, output);
        }
        
        public static VanillaMachineProcessorComponent MachineLoadState(
            Dictionary<string, string> componentStates,
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ElectricPower requestPower)
        {
            var state = componentStates[VanillaMachineSaveComponent.SaveKeyStatic];
            var jsonObject = JsonConvert.DeserializeObject<VanillaMachineJsonObject>(state);
            
            var inputItems = jsonObject.InputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < inputItems.Count; i++)
            {
                vanillaMachineInputInventory.SetItem(i, inputItems[i]);
            }
            
            var outputItems = jsonObject.OutputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < outputItems.Count; i++)
            {
                vanillaMachineOutputInventory.SetItem(i, outputItems[i]);
            }

            if (jsonObject.InputFluids != null)
            {
                for (var i = 0; i < jsonObject.InputFluids.Count && i < vanillaMachineInputInventory.FluidInputSlot.Count; i++)
                {
                    var container = jsonObject.InputFluids[i].ToFluidContainer(vanillaMachineInputInventory.FluidContainerCapacity);
                    vanillaMachineInputInventory.FluidInputSlot[i].Amount = container.Amount;
                    vanillaMachineInputInventory.FluidInputSlot[i].FluidId = container.FluidId;
                }
            }

            if (jsonObject.OutputFluids != null)
            {
                for (var i = 0; i < jsonObject.OutputFluids.Count && i < vanillaMachineOutputInventory.FluidOutputSlot.Count; i++)
                {
                    var container = jsonObject.OutputFluids[i].ToFluidContainer(vanillaMachineOutputInventory.FluidContainerCapacity);
                    vanillaMachineOutputInventory.FluidOutputSlot[i].Amount = container.Amount;
                    vanillaMachineOutputInventory.FluidOutputSlot[i].FluidId = container.FluidId;
                }
            }
            
            var recipe = jsonObject.RecipeGuid == Guid.Empty ? null : MasterHolder.MachineRecipesMaster.GetRecipeElement(jsonObject.RecipeGuid);
            
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