using Core.Block.Blocks;
using Core.Block.Blocks.PowerGenerator;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        private ItemStackFactory _itemStackFactory;

        public VanillaPowerGeneratorTemplate(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int intId)
        {
            var generatorParam= param.Param as PowerGeneratorConfigParam;
            return new VanillaPowerGenerator(param.BlockId,intId,generatorParam.FuelSlot,_itemStackFactory,generatorParam.FuelSettings);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var generatorParam = param.Param as PowerGeneratorConfigParam;
            return new VanillaPowerGenerator(param.BlockId,intId,state,generatorParam.FuelSlot,_itemStackFactory,generatorParam.FuelSettings);
        }
    }
}