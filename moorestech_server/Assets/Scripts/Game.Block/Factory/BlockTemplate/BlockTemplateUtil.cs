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
using Mooresmaster.Model.InventoryConnectsModule;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    public class BlockTemplateUtil
    {
        public static BlockConnectorComponent<IBlockInventory> CreateInventoryConnector(InventoryConnects inventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(
                inventoryConnects.InputConnects, inventoryConnects.OutputConnects, blockPositionInfo);
        }
        
        
        public static (VanillaMachineInputInventory, VanillaMachineOutputInventory) GetMachineIOInventory(
            BlockId blockId,BlockInstanceId blockInstanceId,
            int inputSlotCount,int outputSlotCount,
            BlockConnectorComponent<IBlockInventory> blockConnectorComponent,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            var input = new VanillaMachineInputInventory(
                blockId, inputSlotCount,
                blockInventoryUpdateEvent, blockInstanceId);
            
            var output = new VanillaMachineOutputInventory(
                outputSlotCount, ServerContext.ItemStackFactory, blockInventoryUpdateEvent, blockInstanceId,
                inputSlotCount, blockConnectorComponent);
            
            return (input, output);
        }
        
        public static VanillaMachineProcessorComponent MachineLoadState(
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
            
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.ToList().Find(x => x.MachineRecipeGuid.ToString() == jsonObject.RecipeId);
            
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