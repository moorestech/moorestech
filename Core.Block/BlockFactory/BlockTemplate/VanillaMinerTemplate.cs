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
        private ICheckOreMining _checkOreMining;
        private IOreConfig _oreConfig;
        private ItemStackFactory _itemStackFactory;
        public IBlock New(BlockConfigData param, int intId)
        {
            //TODO 要リファクタ
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
            
            return new VanillaMiner(param.BlockId, intId,requestPower,oreItem,miningTime,minerParam.OutputSlot,_itemStackFactory);
        }

        public IBlock Load(BlockConfigData param, int intId, string state)
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
            
            return new VanillaMiner(param.BlockId, intId,requestPower,oreItem,miningTime,minerParam.OutputSlot,_itemStackFactory);
        }
    }
}