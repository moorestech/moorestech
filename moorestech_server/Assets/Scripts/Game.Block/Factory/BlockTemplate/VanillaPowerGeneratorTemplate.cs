using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var generatorParam = config.Param as PowerGeneratorConfigParam;
            var properties = new VanillaPowerGeneratorProperties(blockInstanceId, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower, generatorParam.FuelSettings, blockPositionInfo, inputConnectorComponent);
            
            var generatorComponent = new VanillaElectricGeneratorComponent(properties);
            
            var components = new List<IBlockComponent>
            {
                generatorComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            BlockConnectorComponent<IBlockInventory> inputConnectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var generatorParam = config.Param as PowerGeneratorConfigParam;
            var properties = new VanillaPowerGeneratorProperties(blockInstanceId, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower, generatorParam.FuelSettings, blockPositionInfo, inputConnectorComponent);
            
            var generatorComponent = new VanillaElectricGeneratorComponent(properties, state);
            
            var components = new List<IBlockComponent>
            {
                generatorComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
    }
    
    public class VanillaPowerGeneratorProperties
    {
        public readonly BlockInstanceId BlockInstanceId;
        public readonly BlockPositionInfo BlockPositionInfo;
        public readonly int FuelItemSlot;
        
        public readonly Dictionary<ItemId, FuelSetting> FuelSettings;
        public readonly ElectricPower InfinityPower;
        public readonly BlockConnectorComponent<IBlockInventory> InventoryInputConnectorComponent;
        public readonly bool IsInfinityPower;
        
        public VanillaPowerGeneratorProperties(BlockInstanceId blockInstanceId, int fuelItemSlot,
            bool isInfinityPower, ElectricPower infinityPower, Dictionary<ItemId, FuelSetting> fuelSettings, BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            BlockInstanceId = blockInstanceId;
            FuelItemSlot = fuelItemSlot;
            IsInfinityPower = isInfinityPower;
            InfinityPower = infinityPower;
            FuelSettings = fuelSettings;
            BlockPositionInfo = blockPositionInfo;
            InventoryInputConnectorComponent = blockConnectorComponent;
        }
    }
}