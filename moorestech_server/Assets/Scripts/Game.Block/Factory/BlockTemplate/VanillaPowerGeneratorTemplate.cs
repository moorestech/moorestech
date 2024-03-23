using System.Collections.Generic;
using Core.Item;
using Game.Block.Interface;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Event;
using Game.World.Interface.DataStore;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public delegate VanillaPowerGeneratorBase LoadGenerator(VanillaPowerGeneratorProperties data, string state);

        public delegate VanillaPowerGeneratorBase NewGenerator(VanillaPowerGeneratorProperties data);

        private readonly IBlockOpenableInventoryUpdateEvent _blockInventoryUpdateEven;


        private readonly ItemStackFactory _itemStackFactory;
        private readonly LoadGenerator _loadGenerator;

        private readonly NewGenerator _newGenerator;

        public VanillaPowerGeneratorTemplate(ItemStackFactory itemStackFactory,
            IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEven, NewGenerator newGenerator,
            LoadGenerator loadGenerator)
        {
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEven = blockInventoryUpdateEven;
            _newGenerator = newGenerator;
            _loadGenerator = loadGenerator;
        }

        public IBlock New(BlockConfigData param, int entityId, long blockHash,BlockPositionInfo blockPositionInfo)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return _newGenerator(new VanillaPowerGeneratorProperties(
                param.BlockId, entityId, blockHash, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower, _itemStackFactory,
                generatorParam.FuelSettings, _blockInventoryUpdateEven));
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state,BlockPositionInfo blockPositionInfo)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return _loadGenerator(new VanillaPowerGeneratorProperties(
                param.BlockId, entityId, blockHash, generatorParam.FuelSlot, generatorParam.IsInfinityPower,
                generatorParam.InfinityPower, _itemStackFactory,
                generatorParam.FuelSettings, _blockInventoryUpdateEven), state);
        }
    }

    public class VanillaPowerGeneratorProperties
    {
        public readonly long BlockHash;
        public readonly int BlockId;
        public readonly IBlockOpenableInventoryUpdateEvent BlockInventoryUpdate;
        public readonly int EntityId;
        public readonly int FuelItemSlot;
        public readonly Dictionary<int, FuelSetting> FuelSettings;
        public readonly int InfinityPower;
        public readonly bool IsInfinityPower;
        public readonly ItemStackFactory ItemStackFactory;

        public VanillaPowerGeneratorProperties(int blockId, int entityId, long blockHash, int fuelItemSlot,
            bool isInfinityPower, int infinityPower, ItemStackFactory itemStackFactory,
            Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate)
        {
            BlockId = blockId;
            EntityId = entityId;
            BlockHash = blockHash;
            FuelItemSlot = fuelItemSlot;
            IsInfinityPower = isInfinityPower;
            InfinityPower = infinityPower;
            ItemStackFactory = itemStackFactory;
            FuelSettings = fuelSettings;
            BlockInventoryUpdate = blockInventoryUpdate;
        }
    }
}