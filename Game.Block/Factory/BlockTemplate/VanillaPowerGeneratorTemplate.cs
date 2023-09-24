using System.Collections.Generic;
using Core.Item;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Event;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public delegate VanillaPowerGeneratorBase NewGenerator(VanillaPowerGeneratorProperties data);
        public delegate VanillaPowerGeneratorBase LoadGenerator(VanillaPowerGeneratorProperties data,string state);
        
        private readonly NewGenerator _newGenerator;
        private readonly LoadGenerator _loadGenerator;
        
        
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IBlockOpenableInventoryUpdateEvent _blockInventoryUpdateEven;

        public VanillaPowerGeneratorTemplate(ItemStackFactory itemStackFactory, IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEven, NewGenerator newGenerator, LoadGenerator loadGenerator)
        {
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEven = blockInventoryUpdateEven;
            _newGenerator = newGenerator;
            _loadGenerator = loadGenerator;
        }

        public IBlock New(BlockConfigData param, int entityId, ulong blockHash)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return _newGenerator(new VanillaPowerGeneratorProperties(
                param.BlockId, entityId,blockHash, generatorParam.FuelSlot,generatorParam.IsInfinityPower,generatorParam.InfinityPower, _itemStackFactory,
                generatorParam.FuelSettings,_blockInventoryUpdateEven));
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return _loadGenerator(new VanillaPowerGeneratorProperties(
                param.BlockId, entityId,blockHash, generatorParam.FuelSlot,generatorParam.IsInfinityPower,generatorParam.InfinityPower, _itemStackFactory,
                generatorParam.FuelSettings,_blockInventoryUpdateEven),state);
        }
    }

    public class VanillaPowerGeneratorProperties
    {
        public readonly int BlockId; 
        public readonly int EntityId; 
        public readonly ulong BlockHash; 
        public readonly int FuelItemSlot;
        public readonly bool IsInfinityPower;
        public readonly int InfinityPower;
        public readonly ItemStackFactory ItemStackFactory;
        public readonly Dictionary<int, FuelSetting> FuelSettings;
        public readonly IBlockOpenableInventoryUpdateEvent BlockInventoryUpdate;

        public VanillaPowerGeneratorProperties(int blockId, int entityId, ulong blockHash, int fuelItemSlot, bool isInfinityPower, int infinityPower, ItemStackFactory itemStackFactory, Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate)
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