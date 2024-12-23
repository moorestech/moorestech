using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var generatorParam = blockMasterElement.BlockParam as ElectricGeneratorBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(generatorParam.InventoryConnectors, blockPositionInfo);
            
            var properties = new VanillaPowerGeneratorProperties(blockInstanceId, generatorParam, blockPositionInfo, inputConnectorComponent);
            var generatorComponent = new VanillaElectricGeneratorComponent(properties);
            
            var components = new List<IBlockComponent>
            {
                generatorComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var generatorParam = blockMasterElement.BlockParam as ElectricGeneratorBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(generatorParam.InventoryConnectors, blockPositionInfo);
            
            var properties = new VanillaPowerGeneratorProperties(blockInstanceId, generatorParam, blockPositionInfo, inputConnectorComponent);
            var generatorComponent = new VanillaElectricGeneratorComponent(componentStates, properties);
            
            var components = new List<IBlockComponent>
            {
                generatorComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
    
    public class VanillaPowerGeneratorProperties
    {
        public readonly BlockInstanceId BlockInstanceId;
        public readonly BlockPositionInfo BlockPositionInfo;
        public readonly int FuelItemSlot;
        
        public readonly Dictionary<ItemId, FuelItemsElement> FuelSettings;
        public readonly ElectricPower InfinityPower;
        public readonly BlockConnectorComponent<IBlockInventory> InventoryInputConnectorComponent;
        public readonly bool IsInfinityPower;
        
        public VanillaPowerGeneratorProperties(BlockInstanceId blockInstanceId,ElectricGeneratorBlockParam param, BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            FuelSettings = new Dictionary<ItemId, FuelItemsElement>();
            
            if (param.FuelItems != null)
            {
                foreach (var fuelItem in param.FuelItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(fuelItem.ItemGuid);
                    FuelSettings.Add(itemId, fuelItem);
                }
            }
            
            BlockInstanceId = blockInstanceId;
            FuelItemSlot = param.FuelItemSlotCount;
            BlockPositionInfo = blockPositionInfo;
            InventoryInputConnectorComponent = blockConnectorComponent;
            
            IsInfinityPower = param.IsInfinityPower;
            InfinityPower = new ElectricPower(param.InfinityPower);
        }
    }
}