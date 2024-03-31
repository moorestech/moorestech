using Core.EnergySystem;
using Core.EnergySystem.Electric;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using UnityEngine;

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
            MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange,
            IWorldBlockDatastore worldBlockDatastore)
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
            var x = blockPlaceEvent.Pos.x;
            var y = blockPlaceEvent.Pos.y;

            //設置されたブロックが発電機か機械以外はスルー処理
            if (!IsElectricMachine(blockPlaceEvent.Pos)) return;

            //最大の電柱の接続範囲を取得探索して接続する
            var startMachineX = x - _maxMachineConnectionRange / 2;
            var startMachineY = y - _maxMachineConnectionRange / 2;
            for (var i = startMachineX; i < startMachineX + _maxMachineConnectionRange; i++)
            for (var j = startMachineY; j < startMachineY + _maxMachineConnectionRange; j++)
            {
                var polePos = new Vector3Int(i, j);
                if (!_worldBlockDatastore.ExistsComponent<IElectricPole>(polePos)) continue;

                //範囲内に電柱がある場合
                //電柱に接続
                var machinePos = blockPlaceEvent.Pos;
                ConnectToElectricPole(polePos, machinePos);
            }
        }

        private bool IsElectricMachine(Vector3Int pos)
        {
            return _worldBlockDatastore.ExistsComponent<TGenerator>(pos) ||
                   _worldBlockDatastore.ExistsComponent<TConsumer>(pos);
        }


        /// <summary>
        ///     電柱のセグメントに機械を接続する
        /// </summary>
        private void ConnectToElectricPole(Vector3Int polePos, Vector3Int machinePos)
        {
            //電柱を取得
            var pole = _worldBlockDatastore.GetBlock<TTransformer>(polePos);
            //その電柱のコンフィグを取得
            var configParam = _blockConfig.GetBlockConfig(((IBlock)pole).BlockId).Param as ElectricPoleConfigParam;
            var range = configParam.machineConnectionRange;

            //その電柱から見て機械が範囲内に存在するか確認
            var poleX = polePos.x;
            var poleY = polePos.y;
            if (poleX - range / 2 > poleX || poleX > poleX + range / 2 || poleY - range / 2 > poleY ||
                poleY > poleY + range / 2) return;

            //在る場合は発電機か機械かを判定して接続
            //発電機を電力セグメントに追加
            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            if (_worldBlockDatastore.ExistsComponent<TGenerator>(machinePos))
                segment.AddGenerator(_worldBlockDatastore.GetBlock<TGenerator>(machinePos));
            else if (_worldBlockDatastore.ExistsComponent<TConsumer>(machinePos))
                segment.AddEnergyConsumer(_worldBlockDatastore.GetBlock<TConsumer>(machinePos));
        }
    }
}