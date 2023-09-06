using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Core.EnergySystem;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler
{
    public class ConnectElectricPoleToElectricSegment<TSegment> where TSegment : EnergySegment, new()
    {
        private readonly IWorldBlockComponentDatastore<IEnergyTransformer> _electricPoleDatastore;
        private readonly IWorldBlockComponentDatastore<IBlockElectricConsumer> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;
        private readonly IBlockConfig _blockConfig;


        public ConnectElectricPoleToElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldBlockComponentDatastore<IEnergyTransformer> electricPoleDatastore,
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            IBlockConfig blockConfig, 
            IWorldBlockComponentDatastore<IBlockElectricConsumer> electricDatastore)
        {
            _electricPoleDatastore = electricPoleDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _blockConfig = blockConfig;
            _electricDatastore = electricDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            //設置されたブロックが電柱だった時の処理
            var x = blockPlaceEvent.Coordinate.X;
            var y = blockPlaceEvent.Coordinate.Y;
            if (!_electricPoleDatastore.ExistsComponentBlock(x, y)) return;
            
            var electricPoleConfigParam = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.BlockId).Param as ElectricPoleConfigParam;

            //電柱と電気セグメントを接続する
            var electricSegment = GetAndConnectElectricSegment(x,y,electricPoleConfigParam,_electricPoleDatastore.GetBlock(x, y));

            //他の機械、発電機を探索して接続する
            ConnectMachine(x, y, electricSegment, electricPoleConfigParam);

        }
        /// <summary>
        /// 他の電柱を探索して接続する
        /// 範囲内の電柱をリストアップする 電柱が１つであればそれに接続、複数ならマージする
        /// 接続したセグメントを返す
        /// </summary>
        private EnergySegment GetAndConnectElectricSegment(
            int x,int y,ElectricPoleConfigParam electricPoleConfigParam,IEnergyTransformer blockElectric)
        {
            //周りの電柱をリストアップする
            var electricPoles = 
                new FindElectricPoleFromPeripheralService().Find(x,y,electricPoleConfigParam,_electricPoleDatastore);

            //接続したセグメントを取得
            var electricSegment = electricPoles.Count switch
            {
                //周りに電柱がないときは新たに電力セグメントを作成する
                0 => _worldEnergySegmentDatastore.CreateEnergySegment(),
                //周りの電柱が1つの時は、その電力セグメントを取得する
                1 => _worldEnergySegmentDatastore.GetEnergySegment(electricPoles[0]),
                //電柱が2つ以上の時はマージする
                _ => ElectricSegmentMergeService.MergeAndSetDatastoreElectricSegments(_worldEnergySegmentDatastore,electricPoles)
            };
            //電柱と電力セグメントを接続する
            electricSegment.AddEnergyTransformer(blockElectric);

            return electricSegment;
        }
        
        /// <summary>
        /// 設置した電柱の周辺にある機械、発電機を探索して接続する
        /// </summary>
        private void ConnectMachine(int x,int y,EnergySegment segment,ElectricPoleConfigParam poleConfigParam)
        {
            var (blocks, generators) = 
                new FindMachineAndGeneratorFromPeripheralService().
                    Find(x, y, poleConfigParam, _electricDatastore, _powerGeneratorDatastore);
            
            //機械と発電機を電力セグメントを接続する
            blocks.ForEach(segment.AddEnergyConsumer);
            generators.ForEach(segment.AddGenerator);
        }
    }
}