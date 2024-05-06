using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = config.CreateConnector(blockPositionInfo);
            var generatorParam = config.Param as PowerGeneratorConfigParam;
            var properties = new VanillaPowerGeneratorProperties(entityId, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower, generatorParam.FuelSettings, blockPositionInfo, inputConnectorComponent);

            var generatorComponent = new VanillaElectricGeneratorComponent(properties);

            var components = new List<IBlockComponent>
            {
                generatorComponent,
                inputConnectorComponent,
            };

            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = config.CreateConnector(blockPositionInfo);
            var generatorParam = config.Param as PowerGeneratorConfigParam;
            var properties = new VanillaPowerGeneratorProperties(entityId, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower, generatorParam.FuelSettings, blockPositionInfo, inputConnectorComponent);

            var generatorComponent = new VanillaElectricGeneratorComponent(properties, state);

            var components = new List<IBlockComponent>
            {
                generatorComponent,
                inputConnectorComponent,
            };

            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
    }

    public class VanillaPowerGeneratorProperties
    {
        public readonly BlockPositionInfo BlockPositionInfo;
        public readonly int EntityId;
        public readonly int FuelItemSlot;

        public readonly Dictionary<int, FuelSetting> FuelSettings;
        public readonly int InfinityPower;
        public readonly BlockConnectorComponent<IBlockInventory> InventoryInputConnectorComponent;
        public readonly bool IsInfinityPower;

        public VanillaPowerGeneratorProperties(int entityId, int fuelItemSlot,
            bool isInfinityPower, int infinityPower, Dictionary<int, FuelSetting> fuelSettings, BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            EntityId = entityId;
            FuelItemSlot = fuelItemSlot;
            IsInfinityPower = isInfinityPower;
            InfinityPower = infinityPower;
            FuelSettings = fuelSettings;
            BlockPositionInfo = blockPositionInfo;
            InventoryInputConnectorComponent = blockConnectorComponent;
        }
    }
}