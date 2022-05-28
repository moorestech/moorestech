using Core.Block.Blocks;
using Core.Block.Blocks.Miner;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Ore;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.WorldMap.EventListener
{
    public class SetMiningItemToMiner
    {
        private readonly IWorldBlockComponentDatastore<IMiner> _minerDatastore;
        private readonly VeinGenerator _veinGenerator;
        private readonly IOreConfig _oreConfig;
        private readonly IBlockConfig _blockConfig;

        public SetMiningItemToMiner(
            IWorldBlockComponentDatastore<IMiner> minerDatastore,
            VeinGenerator veinGenerator,
            IOreConfig oreConfig,
            IBlockPlaceEvent blockPlaceEvent,
            IBlockConfig blockConfig)
        {
            _minerDatastore = minerDatastore;
            _veinGenerator = veinGenerator;
            _oreConfig = oreConfig;
            _blockConfig = blockConfig;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        public void OnBlockPlace(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var x = blockPlaceEventProperties.Coordinate.X;
            var y = blockPlaceEventProperties.Coordinate.Y;
            if (!_minerDatastore.ExistsComponentBlock(x, y)) return;
            
            //鉱石コンフィグを取得
            var oreId = _veinGenerator.GetOreId(x, y);
            if (oreId == OreConst.NoneOreId) return;
            
            
            var oreConfig = _oreConfig.Get(oreId);
            var miner = _minerDatastore.GetBlock(x, y);
            var minerConfig = _blockConfig.GetBlockConfig(((IBlock) miner).BlockId).Param as MinerBlockConfigParam;
            //鉱石IDから鉱石採掘時間を取得
            foreach (var oreSetting in minerConfig.OreSettings)
            {
                if (oreSetting.OreId != oreConfig.OreId) continue;
                miner.SetMiningItem(oreConfig.MiningItemId,oreSetting.MiningTime);
                return;
            }
        }
    }
}