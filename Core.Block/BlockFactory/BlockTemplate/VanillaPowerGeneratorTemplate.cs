using System.Collections.Generic;
using Core.Block.Blocks;
using Core.Block.Blocks.PowerGenerator;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public delegate VanillaPowerGeneratorBase NewGenerator((int blockId, int entityId, ulong blockHash, int fuelItemSlot, ItemStackFactory itemStackFactory, Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate) data);
        public delegate VanillaPowerGeneratorBase LoadGenerator((string state, int blockId, int entityId, ulong blockHash, int fuelItemSlot, ItemStackFactory itemStackFactory, Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate) data);
        
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
            return _newGenerator((param.BlockId, entityId,blockHash, generatorParam.FuelSlot, _itemStackFactory,
                generatorParam.FuelSettings,_blockInventoryUpdateEven));
        }

        public IBlock Load(BlockConfigData param, int entityId, ulong blockHash, string state)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return _loadGenerator((state,param.BlockId, entityId,blockHash, generatorParam.FuelSlot, _itemStackFactory,
                generatorParam.FuelSettings,_blockInventoryUpdateEven));
        }
    }
}