using Game.Block.Config.LoadConfig.Param;
using Game.Context;
using Game.EnergySystem;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電柱やそれに類する動力伝達ブロックが設置されたときに、そのブロックを中心にセグメントを探索して接続する
    /// </summary>
    public class ConnectElectricPoleToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
        where TTransformer : IElectricTransformer
    
    {
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;
        
        
        public ConnectElectricPoleToElectricSegment(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockUpdateProperties updateProperties)
        {
            var pos = updateProperties.Pos;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            //設置されたブロックが電柱だった時の処理
            if (!worldBlockDatastore.ExistsComponent<IElectricTransformer>(pos)) return;
            
            var blockId = updateProperties.BlockData.Block.BlockId;
            var electricPoleConfigParam = ServerContext.BlockConfig.GetBlockConfig(blockId).Param as ElectricPoleConfigParam;
            
            //電柱と電気セグメントを接続する
            var electricSegment = GetAndConnectElectricSegment(pos, electricPoleConfigParam,
                worldBlockDatastore.GetBlock<IElectricTransformer>(pos));
            
            //他の機械、発電機を探索して接続する
            ConnectMachine(pos, electricSegment, electricPoleConfigParam);
        }
        
        /// <summary>
        ///     他の電柱を探索して接続する
        ///     範囲内の電柱をリストアップする 電柱が１つであればそれに接続、複数ならマージする
        ///     接続したセグメントを返す
        /// </summary>
        private EnergySegment GetAndConnectElectricSegment(Vector3Int pos, ElectricPoleConfigParam electricPoleConfigParam, IElectricTransformer blockElectric)
        {
            //周りの電柱をリストアップする
            var electricPoles = FindElectricPoleFromPeripheralService.Find(pos, electricPoleConfigParam);
            
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
            (var blocks, var generators) = FindMachineAndGeneratorFromPeripheralService.Find(pos, poleConfigParam);
            
            //機械と発電機を電力セグメントを接続する
            blocks.ForEach(segment.AddEnergyConsumer);
            generators.ForEach(segment.AddGenerator);
        }
    }
}