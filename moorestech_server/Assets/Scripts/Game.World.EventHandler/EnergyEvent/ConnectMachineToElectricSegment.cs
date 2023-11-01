using Core.EnergySystem;
using Core.EnergySystem.Electric;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電力を生産もしくは消費するブロックが設置されたときに、そのブロックを電柱に接続する
    /// </summary>
    public class ConnectMachineToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer
    {
        private readonly IBlockConfig _blockConfig;
        private readonly int _maxMachineConnectionRange;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;


        public ConnectMachineToElectricSegment(IBlockPlaceEvent blockPlaceEvent,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            IBlockConfig blockConfig,
            MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange, IWorldBlockDatastore worldBlockDatastore)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            _maxMachineConnectionRange = maxElectricPoleMachineConnectionRange.Get();
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            //設置されたブロックが電柱だった時の処理
            var x = blockPlaceEvent.CoreVector2Int.X;
            var y = blockPlaceEvent.CoreVector2Int.Y;

            //設置されたブロックが発電機か機械以外はスルー処理
            if (!IsElectricMachine(x, y)) return;

            //最大の電柱の接続範囲を取得探索して接続する
            var startMachineX = x - _maxMachineConnectionRange / 2;
            var startMachineY = y - _maxMachineConnectionRange / 2;
            for (var i = startMachineX; i < startMachineX + _maxMachineConnectionRange; i++)
            for (var j = startMachineY; j < startMachineY + _maxMachineConnectionRange; j++)
            {
                if (!_worldBlockDatastore.ExistsComponentBlock<IElectricPole>(i, j)) continue;

                //範囲内に電柱がある場合
                //電柱に接続
                ConnectToElectricPole(i, j, x, y);
            }
        }

        private bool IsElectricMachine(int x, int y)
        {
            return _worldBlockDatastore.ExistsComponentBlock<TGenerator>(x, y) ||
                   _worldBlockDatastore.ExistsComponentBlock<TConsumer>(x, y);
        }


        /// <summary>
        ///     電柱のセグメントに機械を接続する
        /// </summary>
        private void ConnectToElectricPole(int poleX, int poleY, int machineX, int machineY)
        {
            //電柱を取得
            var pole = _worldBlockDatastore.GetBlock<TTransformer>(poleX, poleY);
            //その電柱のコンフィグを取得
            var configParam =
                _blockConfig.GetBlockConfig(((IBlock)pole).BlockId).Param as ElectricPoleConfigParam;
            var range = configParam.machineConnectionRange;

            //その電柱から見て機械が範囲内に存在するか確認
            if (poleX - range / 2 > poleX || poleX > poleX + range / 2 || poleY - range / 2 > poleY ||
                poleY > poleY + range / 2) return;

            //在る場合は発電機か機械かを判定して接続
            //発電機を電力セグメントに追加
            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            if (_worldBlockDatastore.ExistsComponentBlock<TGenerator>(machineX, machineY))
                segment.AddGenerator(_worldBlockDatastore.GetBlock<TGenerator>(machineX, machineY));
            else if (_worldBlockDatastore.ExistsComponentBlock<TConsumer>(machineX, machineY)) segment.AddEnergyConsumer(_worldBlockDatastore.GetBlock<TConsumer>(machineX, machineY));
        }
    }
}