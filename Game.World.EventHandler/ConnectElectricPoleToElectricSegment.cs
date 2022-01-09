using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler
{
    public class ConnectElectricPoleToElectricSegment
    {
        private readonly IWorldBlockComponentDatastore<IBlockElectric> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IElectricPole> _electricPoleDatastore;
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly IWorldElectricSegmentDatastore _worldElectricSegmentDatastore;
        private readonly IBlockConfig _blockConfig;


        public ConnectElectricPoleToElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore,
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore,
            IWorldElectricSegmentDatastore worldElectricSegmentDatastore,
            IBlockConfig blockConfig, IWorldBlockComponentDatastore<IBlockElectric> electricDatastore)
        {
            _electricPoleDatastore = electricPoleDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _worldElectricSegmentDatastore = worldElectricSegmentDatastore;
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
            
            var electricPoleConfigParam = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.GetBlockId()).Param as ElectricPoleConfigParam;

            //電柱と電気セグメントを接続する
            var electricSegment = GetAndConnectElectricSegment(x,y,electricPoleConfigParam,_electricPoleDatastore.GetBlock(x, y));

            //他の機械、発電機を探索して接続する
            ConnectMachine(x, y, electricSegment, electricPoleConfigParam.machineConnectionRange);

        }
        /// <summary>
        /// 他の電柱を探索して接続する
        /// 範囲内の電柱をリストアップする 電柱が１つであればそれに接続、複数ならマージする
        /// 接続したセグメントを返す
        /// </summary>
        private ElectricSegment GetAndConnectElectricSegment(int x,int y,ElectricPoleConfigParam electricPoleConfigParam,IElectricPole blockElectric)
        {
            //周りの電柱をリストアップする
            var electricPoles = new List<IElectricPole>();
            
            var poleRange = electricPoleConfigParam.poleConnectionRange;
            _electricPoleDatastore.GetBlock(x, y);
            var startElectricX = x - poleRange / 2;
            var startElectricY = y - poleRange / 2;
            for (int i = startElectricX; i < startElectricX + poleRange; i++)
            {
                for (int j = startElectricY; j < startElectricY + poleRange; j++)
                {
                    //範囲内に電柱がある場合 ただし自身のブロックは除く
                    if (!_electricPoleDatastore.ExistsComponentBlock(i, j) || i == x && j == y) continue;

                    //電柱を追加
                    electricPoles.Add(_electricPoleDatastore.GetBlock(i, j));
                }
            }
            
            
            

            //接続したセグメントを取得
            var electricSegment = electricPoles.Count switch
            {
                //周りに電柱がないときは新たに電力セグメントを作成する
                0 => _worldElectricSegmentDatastore.CreateElectricSegment(),
                //周りの電柱が1つの時は、その電力セグメントを取得する
                1 => _worldElectricSegmentDatastore.GetElectricSegment(electricPoles[0]),
                //電柱が2つ以上の時はマージする
                _ => _worldElectricSegmentDatastore.MergedElectricSegments(electricPoles)
            };
            //電柱と電力セグメントを接続する
            electricSegment.AddElectricPole(blockElectric);

            return electricSegment;
        }
        
        /// <summary>
        /// 設置した電柱の周辺にある機械、発電機を探索して接続する
        /// </summary>
        private void ConnectMachine(int x,int y,ElectricSegment electricSegment,int machineRange)
        {
            var startMachineX = x - machineRange / 2;
            var startMachineY = y - machineRange / 2;
            for (int i = startMachineX; i < startMachineX + machineRange; i++)
            {
                for (int j = startMachineY; j < startMachineY + machineRange; j++)
                {
                    //範囲内に機械がある場合
                    if (_electricDatastore.ExistsComponentBlock(i, j))
                    {
                        //機械を電力セグメントに追加
                        electricSegment.AddBlockElectric(_electricDatastore.GetBlock(i, j));
                    }

                    //範囲内に発電機がある場合
                    if (_powerGeneratorDatastore.ExistsComponentBlock(i, j))
                    {
                        //機械を電力セグメントに追加
                        electricSegment.AddGenerator(_powerGeneratorDatastore.GetBlock(i, j));
                    }
                }
            }
        }
    }
}