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
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate
{
    public class BlockTemplateUtil
    {
        public static BlockConnectorComponent<IBlockInventory> CreateInventoryConnector(InventoryConnects inventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(
                BlockConnectorAdapter.FromInputConnects(inventoryConnects.InputConnects),
                BlockConnectorAdapter.FromOutputConnects(inventoryConnects.OutputConnects),
                blockPositionInfo);
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
            
            // ElectricMachineBlockParamから流体関連のパラメータを取得
            var inputTankCount = 0;
            var outputTankCount = 0;
            var innerTankCapacity = 0f;
            
            if (machineParam is ElectricMachineBlockParam electricMachineParam)
            {
                inputTankCount = electricMachineParam.InputTankCount;
                outputTankCount = electricMachineParam.OutputTankCount;
                innerTankCapacity = electricMachineParam.InnerTankCapacity;
            }
            
            var input = new VanillaMachineInputInventory(
                blockId,
                inputSlotCount,
                inputTankCount,
                innerTankCapacity,
                blockInventoryUpdateEvent,
                blockInstanceId
            );
            
            var output = new VanillaMachineOutputInventory(
                outputSlotCount, outputTankCount, innerTankCapacity, ServerContext.ItemStackFactory, blockInventoryUpdateEvent, blockInstanceId,
                inputSlotCount, blockConnectorComponent);
            
            return (input, output);
        }
        
        public static VanillaMachineProcessorComponent MachineLoadState(Dictionary<string, string> componentStates,
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            ElectricPower requestPower, BlockMasterElement blockMasterElement)
        {
            var state = componentStates[VanillaMachineSaveComponent.SaveKeyStatic];
            var jsonObject = JsonConvert.DeserializeObject<VanillaMachineJsonObject>(state);

            // セーブデータからのロード時はイベントを発火しない（ブロックがまだWorldBlockDatastoreに登録されていないため）
            // Do not invoke events when loading from save data (block is not yet registered in WorldBlockDatastore)
            var inputItems = jsonObject.InputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < inputItems.Count; i++)
            {
                if (vanillaMachineInputInventory.InputSlot.Count <= i)
                {
                    Debug.LogError($"ロードするデータのインベントリサイズが超過しています。一部のアイテムは消失します。ブロック名:{blockMasterElement.Name} Guid:{blockMasterElement.BlockGuid}");
                    break;
                }
                vanillaMachineInputInventory.SetItemWithoutEvent(i, inputItems[i]);
            }

            var outputItems = jsonObject.OutputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < outputItems.Count; i++)
            {
                if (vanillaMachineOutputInventory.OutputSlot.Count <= i)
                {
                    Debug.LogError($"ロードするデータのインベントリサイズが超過しています。一部のアイテムは消失します。ブロック名:{blockMasterElement.Name} Guid:{blockMasterElement.BlockGuid}");
                    break;
                }
                vanillaMachineOutputInventory.SetItemWithoutEvent(i, outputItems[i]);
            }
            
            // Load fluid data if present
            if (jsonObject.InputFluidSlot != null)
            {
                for (var i = 0; i < jsonObject.InputFluidSlot.Count && i < vanillaMachineInputInventory.FluidInputSlot.Count; i++)
                {
                    var fluidData = jsonObject.InputFluidSlot[i];
                    vanillaMachineInputInventory.FluidInputSlot[i].FluidId = fluidData.FluidId;
                    vanillaMachineInputInventory.FluidInputSlot[i].Amount = fluidData.Amount;
                }
            }
            
            if (jsonObject.OutputFluidSlot != null)
            {
                for (var i = 0; i < jsonObject.OutputFluidSlot.Count && i < vanillaMachineOutputInventory.FluidOutputSlot.Count; i++)
                {
                    var fluidData = jsonObject.OutputFluidSlot[i];
                    vanillaMachineOutputInventory.FluidOutputSlot[i].FluidId = fluidData.FluidId;
                    vanillaMachineOutputInventory.FluidOutputSlot[i].Amount = fluidData.Amount;
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