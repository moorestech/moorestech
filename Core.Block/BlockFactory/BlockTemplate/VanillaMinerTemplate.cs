using Core.Block.Blocks;
using Core.Block.Blocks.Miner;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Item;
using Core.Item.Util;
using Core.Ore;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class VanillaMinerTemplate : IBlockTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaMinerTemplate(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int intId)
        {
            var (requestPower, outputSlot) = GetData(param, intId);
            
            return new VanillaMiner(param.BlockId, intId, requestPower, outputSlot,
                _itemStackFactory);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var (requestPower, outputSlot) = GetData(param, intId);
            
            return new VanillaMiner(state, param.BlockId, intId, requestPower,
                outputSlot,
                _itemStackFactory);
        }

        private (int, int) GetData(BlockConfigData param, int intId)
        {
            var minerParam = param.Param as MinerBlockConfigParam;
            
            var oreItem = ItemConst.NullItemId;
            var requestPower = 0;
            var miningTime = int.MaxValue;
            
            return (requestPower, minerParam.OutputSlot);
        }
    }
}