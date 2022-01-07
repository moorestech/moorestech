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
        private readonly ICheckOreMining _checkOreMining;
        private readonly IOreConfig _oreConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public VanillaMinerTemplate(ICheckOreMining checkOreMining, IOreConfig oreConfig,
            ItemStackFactory itemStackFactory)
        {
            _checkOreMining = checkOreMining;
            _oreConfig = oreConfig;
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int intId)
        {
            var (requestPower, oreItem, miningTime, outputSlot) = GetData(param, intId);
            
            return new VanillaMiner(param.BlockId, intId, requestPower, oreItem, miningTime, outputSlot,
                _itemStackFactory);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var (requestPower, oreItem, miningTime, outputSlot) = GetData(param, intId);
            
            return new VanillaMiner(state, param.BlockId, intId, requestPower, oreItem, miningTime,
                outputSlot,
                _itemStackFactory);
        }

        private (int, int, int,int) GetData(BlockConfigData param, int intId)
        {
            var minerParam = param.Param as MinerBlockConfigParam;
            
            var oreId = _checkOreMining.Check(intId);
            var oreItem = ItemConst.NullItemId;
            var requestPower = 0;
            var miningTime = int.MaxValue;

            if (minerParam.OreSettings.Exists(o => o.OreId == oreId))
            {
                requestPower = minerParam.RequiredPower;
                miningTime = minerParam.OreSettings.Find(o => o.OreId == oreId).MiningTime;
                oreItem = _oreConfig.OreIdToItemId(oreId);
            }
            
            return (requestPower, oreItem, miningTime, minerParam.OutputSlot);
        }
    }
}