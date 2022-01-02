using System.Collections.Generic;
using Core.Block;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using World.Service;

namespace World.EventListener
{
    public class ConnectMachineToElectricSegment
    {
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly IWorldBlockComponentDatastore<IBlockElectric> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IElectricPole> _electricPoleDatastore;
        private readonly IWorldElectricSegmentDatastore _worldElectricSegmentDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly int _maxMachineConnectionRange;
        
        
        public ConnectMachineToElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore,
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore,
            IWorldElectricSegmentDatastore worldElectricSegmentDatastore,
            IBlockConfig blockConfig,
            MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange, 
            IWorldBlockComponentDatastore<IBlockElectric> electricDatastore)
        {
            _electricPoleDatastore = electricPoleDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _worldElectricSegmentDatastore = worldElectricSegmentDatastore;
            _blockConfig = blockConfig;
            _electricDatastore = electricDatastore;
            _maxMachineConnectionRange = maxElectricPoleMachineConnectionRange.Get();
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            //設置されたブロックが電柱だった時の処理
            var x = blockPlaceEvent.Coordinate.X;
            var y = blockPlaceEvent.Coordinate.Y;

            //設置されたブロックが発電機だった時の処理
            if (!_powerGeneratorDatastore.ExistsComponentBlock(x, y) ||!_electricDatastore.ExistsComponentBlock(x, y) ) return;
            
            //最大の電柱の接続範囲を取得探索して接続する
            var startMachineX = x - _maxMachineConnectionRange / 2;
            var startMachineY = y - _maxMachineConnectionRange / 2;
            for (int i = startMachineX; i < startMachineX + _maxMachineConnectionRange; i++)
            {
                for (int j = startMachineY; j < startMachineY + _maxMachineConnectionRange; j++)
                {
                    //範囲内に電柱がある場合
                    if (!_electricPoleDatastore.ExistsComponentBlock(i, j)) continue;
                    //電柱に接続
                    ConnectGenerator(i, j);
                }
            }
        }
        
        /// <summary>
        /// 電柱のセグメントに発電機を接続する
        /// </summary>
        private void ConnectGenerator(int x, int y)
        {
            //電柱を取得
            var pole = _electricPoleDatastore.GetBlock(x,y);
            //その電柱から見て機械が範囲内に存在するか確認
            var configParam = _blockConfig.GetBlockConfig(((IBlock)pole).GetBlockId()).Param as ElectricPoleConfigParam;
            var range = configParam.machineConnectionRange;
            if (x - range / 2 <= x && x <= x + range / 2 && y - range / 2 <= y && y <= y + range / 2)
            {
                //発電機を電力セグメントに追加
                _worldElectricSegmentDatastore.GetElectricSegment(pole).AddGenerator(_powerGeneratorDatastore.GetBlock(x,y));
            }
        }
    }
    
}