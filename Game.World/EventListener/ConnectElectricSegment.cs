using Core.Block;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using World.DataStore;
using World.Service;

namespace World.EventListener
{
    public class ConnectElectricSegment
    {
        private readonly IWorldBlockComponentDatastore<IBlockElectric> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IElectricPole> _electricPoleDatastore;
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly IWorldElectricSegmentDatastore _worldElectricSegmentDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly int _maxMachineConnectionRange;

        public ConnectElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldBlockComponentDatastore<IBlockElectric> electricDatastore,
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore,
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore,
            IWorldElectricSegmentDatastore worldElectricSegmentDatastore,
            IBlockConfig blockConfig,
            MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange)
        {
            _electricDatastore = electricDatastore;
            _electricPoleDatastore = electricPoleDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _worldElectricSegmentDatastore = worldElectricSegmentDatastore;
            _blockConfig = blockConfig;
            _maxMachineConnectionRange = maxElectricPoleMachineConnectionRange.Get();
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
                var startElectricX = x - poleRange / 2;
                var startElectricY = y - poleRange / 2;
                for (int i = startElectricX; i < startElectricX + poleRange; i++)
                {
                    for (int j = startElectricY; j < startElectricY + poleRange; j++)
                    {
                        //範囲内に電柱がある場合、自身のブロックは除く
                        if (_electricPoleDatastore.ExistsComponentBlock(i,j) && !(i == x && j == y))
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
                    electricSegment = _worldElectricSegmentDatastore.CreateElectricSegment();
                    electricSegment.AddElectricPole(blockPlaceEvent.Block as IElectricPole);
                }


                //他の機械を探索して接続する
                var machineRange = electric.machineConnectionRange;
                
                var startMachineX =  x - machineRange / 2;
                var startMachineY = y - machineRange / 2;
                for (int i = startMachineX; i < startMachineX + machineRange; i++)
                {
                    for (int j = startMachineY; j < startMachineY + machineRange; j++)
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
            
            //設置されたブロックが電力機械だった時の処理
            if (_electricDatastore.ExistsComponentBlock(x,y))
            {
                //最大の電柱の接続範囲を取得探索して接続する
                var startMachineX = x - _maxMachineConnectionRange / 2;
                var startMachineY = y - _maxMachineConnectionRange / 2;
                for (int i = startMachineX; i < startMachineX + _maxMachineConnectionRange; i++)
                {
                    for (int j = startMachineY; j < startMachineY + _maxMachineConnectionRange; j++)
                    {
                        //範囲内に電柱がある場合
                        if (!_electricPoleDatastore.ExistsComponentBlock(i, j)) continue;
                        
                        //電柱を取得
                        var pole = _electricPoleDatastore.GetBlock(i,j);
                        //その電柱から見て機械が範囲内に存在するか確認
                        var configParam = _blockConfig.GetBlockConfig(((IBlock)pole).GetBlockId()).Param as ElectricPoleConfigParam;
                        var range = configParam.machineConnectionRange;
                        if (i - range / 2 <= x && x <= i + range / 2 && j - range / 2 <= y && y <= j + range / 2)
                        {
                            //機械を電力セグメントに追加
                            _worldElectricSegmentDatastore.GetElectricSegment(pole).AddBlockElectric(_electricDatastore.GetBlock(x,y));
                        }
                    }
                }
                
                
            }
            
            
            //設置されたブロックが発電機だった時の処理
            if (_powerGeneratorDatastore.ExistsComponentBlock(x,y))
            {
                //最大の電柱の接続範囲を取得探索して接続する
                var startMachineX = x - _maxMachineConnectionRange / 2;
                var startMachineY = y - _maxMachineConnectionRange / 2;
                for (int i = startMachineX; i < startMachineX + _maxMachineConnectionRange; i++)
                {
                    for (int j = startMachineY; j < startMachineY + _maxMachineConnectionRange; j++)
                    {
                        //範囲内に電柱がある場合
                        if (!_electricPoleDatastore.ExistsComponentBlock(i, j)) continue;
                        
                        //電柱を取得
                        var pole = _electricPoleDatastore.GetBlock(i,j);
                        //その電柱から見て機械が範囲内に存在するか確認
                        var configParam = _blockConfig.GetBlockConfig(((IBlock)pole).GetBlockId()).Param as ElectricPoleConfigParam;
                        var range = configParam.machineConnectionRange;
                        if (i - range / 2 <= x && x <= i + range / 2 && j - range / 2 <= y && y <= j + range / 2)
                        {
                            //発電機を電力セグメントに追加
                            _worldElectricSegmentDatastore.GetElectricSegment(pole).AddGenerator(_powerGeneratorDatastore.GetBlock(x,y));
                        }
                    }
                }
            }
        }
    }
}