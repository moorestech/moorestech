using Core.Block.Blocks;
using Core.Block.Blocks.Miner;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Ore;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.WorldMap.EventListener
{
    /// <summary>
    /// 採掘機が設置されたらその採掘機に下にある鉱石の種類を設定する
    /// </summary>
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
            
            //マイナー自体の設定からその採掘機が鉱石を採掘するのに必要な時間を取得し、設定する
            foreach (var oreSetting in minerConfig.OreSettings)
            {
                //採掘可能な鉱石設定の中にあるか？
                if (oreSetting.OreId != oreConfig.OreId) continue;
                
                //採掘時間、アイテムを設定する
                var itemId = _oreConfig.OreIdToItemId(oreConfig.OreId);
                miner.SetMiningItem(itemId,oreSetting.MiningTime);
                return;
            }
        }
    }
}