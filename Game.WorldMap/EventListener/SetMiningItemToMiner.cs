using Core.Ore;
using Game.Block.Blocks.Miner;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.WorldMap.EventListener
{
    /// <summary>
    ///     
    ///     TODO 
    /// </summary>
    public class SetMiningItemToMiner
    {
        private readonly IBlockConfig _blockConfig;
        private readonly IOreConfig _oreConfig;
        private readonly VeinGenerator _veinGenerator;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public SetMiningItemToMiner(
            VeinGenerator veinGenerator,
            IOreConfig oreConfig,
            IBlockPlaceEvent blockPlaceEvent,
            IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore)
        {
            _veinGenerator = veinGenerator;
            _oreConfig = oreConfig;
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        public void OnBlockPlace(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var x = blockPlaceEventProperties.Coordinate.X;
            var y = blockPlaceEventProperties.Coordinate.Y;
            if (!_worldBlockDatastore.ExistsComponentBlock<IMiner>(x, y)) return;

            
            var oreId = _veinGenerator.GetOreId(x, y);
            if (oreId == OreConst.NoneOreId) return;


            var oreConfig = _oreConfig.Get(oreId);
            var miner = _worldBlockDatastore.GetBlock<IMiner>(x, y);
            var minerConfig = _blockConfig.GetBlockConfig(((IBlock)miner).BlockId).Param as MinerBlockConfigParam;

            
            foreach (var oreSetting in minerConfig.OreSettings)
            {
                
                if (oreSetting.OreId != oreConfig.OreId) continue;

                
                var itemId = _oreConfig.OreIdToItemId(oreConfig.OreId);
                miner.SetMiningItem(itemId, oreSetting.MiningTime);
                return;
            }
        }
    }
}