using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using World.Service;

namespace World.EventListener
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
            var config = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.GetBlockId());
            var electric = config.Param as ElectricPoleConfigParam;


            //他の電柱を探索して接続する
            //範囲内の電柱をリストアップする 電柱が１つであればそれに接続、複数ならマージする
            var poleRange = electric.poleConnectionRange;
            _electricPoleDatastore.GetBlock(x, y);
            var startElectricX = x - poleRange / 2;
            var startElectricY = y - poleRange / 2;
            var electricPoles = new List<IElectricPole>();
            for (int i = startElectricX; i < startElectricX + poleRange; i++)
            {
                for (int j = startElectricY; j < startElectricY + poleRange; j++)
                {
                    //範囲内に電柱がある場合、自身のブロックは除く
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
                _ => _worldElectricSegmentDatastore.MergedElectricSegments(electricPoles)
            };
            //電柱と電力セグメントを接続する
            electricSegment.AddElectricPole(blockPlaceEvent.Block as IElectricPole);


            //他の機械、発電機を探索して接続する
            var machineRange = electric.machineConnectionRange;

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