using System.Collections.Generic;
using Game.Block.Blocks.Miner;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Context;
using Game.Map.Interface.Vein;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.WorldMap.EventListener
{
    /// <summary>
    ///     採掘機が設置されたらその採掘機に下にある鉱石の種類を設定する
    ///     TODO 削除する
    /// </summary>
    public class SetMiningItemToMiner
    {
        private readonly IBlockConfig _blockConfig;
        private readonly IMapVeinDatastore _mapVeinDatastore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public SetMiningItemToMiner(IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore, IMapVeinDatastore mapVeinDatastore)
        {
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            _mapVeinDatastore = mapVeinDatastore;
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }

        private void OnBlockRemove(BlockUpdateProperties updateProperties)
        {
            var pos = updateProperties.Pos;

            //採掘機が設置されたか
            if (!_worldBlockDatastore.ExistsComponent<IMiner>(pos)) return;

            //採掘機の下に鉱脈があるか
            List<IMapVein> vein = _mapVeinDatastore.GetOverVeins(pos);
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