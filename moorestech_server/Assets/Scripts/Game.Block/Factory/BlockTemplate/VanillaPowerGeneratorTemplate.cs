using System.Collections.Generic;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Component;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public delegate VanillaPowerGeneratorBase LoadGenerator(VanillaPowerGeneratorProperties data, string state);

        public delegate VanillaPowerGeneratorBase NewGenerator(VanillaPowerGeneratorProperties data);

        private readonly LoadGenerator _loadGenerator;
        private readonly NewGenerator _newGenerator;

        public VanillaPowerGeneratorTemplate(NewGenerator newGenerator, LoadGenerator loadGenerator)
        {
            _newGenerator = newGenerator;
            _loadGenerator = loadGenerator;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = GetComponent(blockPositionInfo);
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return _newGenerator(new VanillaPowerGeneratorProperties(
                param.BlockId, entityId, blockHash, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower, generatorParam.FuelSettings, blockPositionInfo, inputConnectorComponent));
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var inputConnectorComponent = GetComponent(blockPositionInfo);
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return _loadGenerator(new VanillaPowerGeneratorProperties(
                param.BlockId, entityId, blockHash, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower,
                generatorParam.FuelSettings, blockPositionInfo, inputConnectorComponent), state);
        }

        private InventoryInputConnectorComponent GetComponent(BlockPositionInfo blockPositionInfo)
        {
            return new InventoryInputConnectorComponent(
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { },
                    new[] { VanillaBlockType.BeltConveyor }), blockPositionInfo);
        }
    }

    public class VanillaPowerGeneratorProperties
    {
        public readonly long BlockHash;
        public readonly int BlockId;
        public readonly BlockPositionInfo BlockPositionInfo;
        public readonly int EntityId;
        public readonly int FuelItemSlot;

        public readonly Dictionary<int, FuelSetting> FuelSettings;
        public readonly int InfinityPower;
        public readonly InventoryInputConnectorComponent InventoryInputConnectorComponent;
        public readonly bool IsInfinityPower;

        public VanillaPowerGeneratorProperties(int blockId, int entityId, long blockHash, int fuelItemSlot,
            bool isInfinityPower, int infinityPower, Dictionary<int, FuelSetting> fuelSettings, BlockPositionInfo blockPositionInfo, InventoryInputConnectorComponent inventoryInputConnectorComponent)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
            FuelItemSlot = fuelItemSlot;
            IsInfinityPower = isInfinityPower;
            InfinityPower = infinityPower;
            FuelSettings = fuelSettings;
            BlockPositionInfo = blockPositionInfo;
            InventoryInputConnectorComponent = inventoryInputConnectorComponent;
        }
    }
}