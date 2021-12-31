using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.PowerGenerator;
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
            var powerGeneratorConfig = param.Param as PowerGeneratorConfigParam;
            return new VanillaPowerGenerator(param.BlockId,intId,powerGeneratorConfig.FuelSlot,_itemStackFactory);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var powerGeneratorConfig = param.Param as PowerGeneratorConfigParam;
            return new VanillaPowerGenerator(param.BlockId,intId,powerGeneratorConfig.FuelSlot,_itemStackFactory);
        }
    }
}