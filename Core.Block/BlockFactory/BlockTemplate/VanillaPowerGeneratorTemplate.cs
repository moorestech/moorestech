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
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IBlockOpenableInventoryUpdateEvent _blockInventoryUpdateEven;

        public VanillaPowerGeneratorTemplate(ItemStackFactory itemStackFactory, IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEven)
        {
            _itemStackFactory = itemStackFactory;
            _blockInventoryUpdateEven = blockInventoryUpdateEven;
        }

        public IBlock New(BlockConfigData param, int entityId)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return new VanillaPowerGenerator(param.BlockId, entityId, generatorParam.FuelSlot, _itemStackFactory,
                generatorParam.FuelSettings,_blockInventoryUpdateEven);
        }

        public IBlock Load(BlockConfigData param, int entityId, string state)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return new VanillaPowerGenerator(param.BlockId, entityId, state, generatorParam.FuelSlot, _itemStackFactory,
                generatorParam.FuelSettings,_blockInventoryUpdateEven);
        }
    }
}