using Core.Item.Config;
using Game.Block.Blocks.Miner;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Map.Interface.Vein;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.WorldMap.EventListener
{
    /// <summary>
    ///     採掘機が設置されたらその採掘機に下にある鉱石の種類を設定する
    ///     TODO 削除する
    /// </summary>
    public class SetMiningItemToMiner
    {
        private readonly IBlockConfig _blockConfig;
        private readonly IItemConfig _itemConfig;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IMapVeinDatastore _mapVeinDatastore;

        public SetMiningItemToMiner(
            IBlockPlaceEvent blockPlaceEvent,
            IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore, IMapVeinDatastore mapVeinDatastore, IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            _mapVeinDatastore = mapVeinDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var pos = blockPlaceEventProperties.Pos;
            
            //採掘機が設置されたか
            if (!_worldBlockDatastore.ExistsComponentBlock<IMiner>(pos)) return;
            
            //採掘機の下に鉱脈があるか
            var vein = _mapVeinDatastore.GetOverVeins(pos);
            if (vein.Count == 0) return;


            var miner = _worldBlockDatastore.GetBlock<IMiner>(pos);
            var minerConfig = _blockConfig.GetBlockConfig(((IBlock)miner).BlockId).Param as MinerBlockConfigParam;

            //マイナー自体の設定からその採掘機が鉱石を採掘するのに必要な時間を取得し、設定する
            foreach (var mineSetting in minerConfig.MineItemSettings)
            {
                //採掘可能な鉱石設定の中にあるか？
                if (!vein.Exists(v => v.VeinItemId == mineSetting.ItemId)) return;
                
                //採掘時間、アイテムを設定する
                miner.SetMiningItem(mineSetting.ItemId, mineSetting.MiningTime);
                return;
            }
        }
    }
}