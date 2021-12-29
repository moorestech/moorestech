using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using World.DataStore;
using World.Util;

namespace World.EventListener
{
    public class ConnectElectricSegment
    {
        private readonly IWorldBlockComponentDatastore<IBlockElectric> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IElectricPole> _electricPoleDatastore;
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly WorldElectricSegmentDatastore _worldElectricSegmentDatastore;
        private readonly IBlockConfig _blockConfig;

        public ConnectElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldBlockComponentDatastore<IBlockElectric> electricDatastore,
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore,
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore,
            WorldElectricSegmentDatastore worldElectricSegmentDatastore,
            IBlockConfig blockConfig)
        {
            _electricDatastore = electricDatastore;
            _electricPoleDatastore = electricPoleDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _worldElectricSegmentDatastore = worldElectricSegmentDatastore;
            _blockConfig = blockConfig;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            //設置されたブロックが電柱だった時の処理
            var x = blockPlaceEvent.Coordinate.X;
            var y = blockPlaceEvent.Coordinate.Y;
            if (_electricPoleDatastore.ExistsComponentBlock(x,y))
            {
                var config = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.GetBlockId());
                var electric = config.Param as ElectricPoleConfigParam;
                
                var isConnect = false;
                
                //他の電柱を探索して接続する
                var poleRange = electric.poleConnectionRange;
                _electricPoleDatastore.GetBlock(x, y);
                var firstElectricX = poleRange / 2 + x;
                var firstElectricY = poleRange / 2 + y;
                for (int i = firstElectricX; i < firstElectricX + poleRange; i++)
                {
                    for (int j = firstElectricY; j < firstElectricY + poleRange; j++)
                    {
                        //範囲内に電柱がある場合
                        if (_electricPoleDatastore.ExistsComponentBlock(i,j))
                        {
                            //電柱を取得
                            var pole = _electricPoleDatastore.GetBlock(i,j);
                            //電柱からその電柱が所属している電気セグメントを取得
                            var segment = _worldElectricSegmentDatastore.GetElectricSegment(pole);
                            //その電気セグメントに電柱を追加
                            segment.AddElectricPole(blockPlaceEvent.Block as IElectricPole);

                            isConnect = true;
                        }
                    }
                }
                
                //接続したセグメントを取得
                ElectricSegment electricSegment = null;
                //周りに電柱がないときは新たに電力セグメントを作成する
                if (isConnect)
                {
                    electricSegment =
                        _worldElectricSegmentDatastore.GetElectricSegment(blockPlaceEvent.Block as IElectricPole);
                }
                else
                {
                    electricSegment = _worldElectricSegmentDatastore.CreateElectricSegment(IntId.NewIntId());
                }


                //他の機械を探索して接続する
                var machineRange = electric.machineConnectionRange;
                var firstMachineX = machineRange / 2 + x;
                var firstMachineY = machineRange / 2 + y;
                for (int i = firstMachineX; i < firstMachineX + machineRange; i++)
                {
                    for (int j = firstMachineY; j < firstMachineY + machineRange; j++)
                    {
                        //範囲内に機械がある場合
                        if (_electricDatastore.ExistsComponentBlock(i,j))
                        {
                            //機械を電力セグメントに追加
                            electricSegment.AddBlockElectric(_electricDatastore.GetBlock(i,j));
                        }
                    }
                }
            }
            
            //TODO 設置されたブロックが電力機械or発電機だった時の処理
        }
    }
}