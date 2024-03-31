using System.Collections.Generic;
using Server.Core.EnergySystem;
using Server.Core.EnergySystem.Electric;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電柱やそれに類する動力伝達ブロックが設置されたときに、そのブロックを中心にセグメントを探索して接続する
    /// </summary>
    public class ConnectElectricPoleToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer

    {
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;


        public ConnectElectricPoleToElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            //設置されたブロックが電柱だった時の処理
            if (!_worldBlockDatastore.ExistsComponent<IEnergyTransformer>(blockPlaceEvent.Pos)) return;

            var electricPoleConfigParam =
                _blockConfig.GetBlockConfig(blockPlaceEvent.Block.BlockId).Param as ElectricPoleConfigParam;

            //電柱と電気セグメントを接続する
            var electricSegment = GetAndConnectElectricSegment(blockPlaceEvent.Pos, electricPoleConfigParam,
                _worldBlockDatastore.GetBlock<IEnergyTransformer>(blockPlaceEvent.Pos));

            //他の機械、発電機を探索して接続する
            ConnectMachine(blockPlaceEvent.Pos, electricSegment, electricPoleConfigParam);
        }

        /// <summary>
        ///     他の電柱を探索して接続する
        ///     範囲内の電柱をリストアップする 電柱が１つであればそれに接続、複数ならマージする
        ///     接続したセグメントを返す
        /// </summary>
        private EnergySegment GetAndConnectElectricSegment(Vector3Int pos, ElectricPoleConfigParam electricPoleConfigParam, IEnergyTransformer blockElectric)
        {
            //周りの電柱をリストアップする
            List<IEnergyTransformer> electricPoles =
                FindElectricPoleFromPeripheralService.Find(pos, electricPoleConfigParam, _worldBlockDatastore);

            //接続したセグメントを取得
            var electricSegment = electricPoles.Count switch
            {
                //周りに電柱がないときは新たに電力セグメントを作成する
                0 => _worldEnergySegmentDatastore.CreateEnergySegment(),
                //周りの電柱が1つの時は、その電力セグメントを取得する
                1 => _worldEnergySegmentDatastore.GetEnergySegment(electricPoles[0]),
                //電柱が2つ以上の時はマージする
                _ => ElectricSegmentMergeService.MergeAndSetDatastoreElectricSegments(_worldEnergySegmentDatastore,
                    electricPoles),
            };
            //電柱と電力セグメントを接続する
            electricSegment.AddEnergyTransformer(blockElectric);

            return electricSegment;
        }

        /// <summary>
        ///     設置した電柱の周辺にある機械、発電機を探索して接続する
        /// </summary>
        private void ConnectMachine(Vector3Int pos, EnergySegment segment, ElectricPoleConfigParam poleConfigParam)
        {
            (List<IBlockElectricConsumer> blocks, List<IElectricGenerator> generators) =
                FindMachineAndGeneratorFromPeripheralService.Find(pos, poleConfigParam, _worldBlockDatastore);

            //機械と発電機を電力セグメントを接続する
            blocks.ForEach(segment.AddEnergyConsumer);
            generators.ForEach(segment.AddGenerator);
        }
    }
}