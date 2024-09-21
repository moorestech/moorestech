using Core.Master;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電力を生産もしくは消費するブロックが設置されたときに、そのブロックを電柱に接続する
    /// </summary>
    public class ConnectMachineToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
        where TTransformer : IElectricTransformer
    {
        private readonly int _maxMachineConnectionRange;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;
        
        
        public ConnectMachineToElectricSegment(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore, MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _maxMachineConnectionRange = maxElectricPoleMachineConnectionRange.Get();
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockUpdateProperties updateProperties)
        {
            //設置されたブロックが電柱だった時の処理
            var pos = updateProperties.Pos;
            var x = updateProperties.Pos.x;
            var y = updateProperties.Pos.y;
            
            //設置されたブロックが発電機か機械以外はスルー処理
            if (!IsElectricMachine(pos)) return;
            
            //最大の電柱の接続範囲を取得探索して接続する
            var startMachineX = x - _maxMachineConnectionRange / 2;
            var startMachineY = y - _maxMachineConnectionRange / 2;
            for (var i = startMachineX; i < startMachineX + _maxMachineConnectionRange; i++)
            for (var j = startMachineY; j < startMachineY + _maxMachineConnectionRange; j++)
            {
                var polePos = new Vector3Int(i, j);
                if (!ServerContext.WorldBlockDatastore.ExistsComponent<IElectricTransformer>(polePos)) continue;
                
                //範囲内に電柱がある場合
                //電柱に接続
                ConnectToElectricPole(polePos, pos);
            }
        }
        
        private bool IsElectricMachine(Vector3Int pos)
        {
            return ServerContext.WorldBlockDatastore.ExistsComponent<TGenerator>(pos) ||
                   ServerContext.WorldBlockDatastore.ExistsComponent<TConsumer>(pos);
        }
        
        
        /// <summary>
        ///     電柱のセグメントに機械を接続する
        /// </summary>
        private void ConnectToElectricPole(Vector3Int polePos, Vector3Int machinePos)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            
            //電柱を取得
            var block = ServerContext.WorldBlockDatastore.GetBlock(polePos);
            var pole = block.GetComponent<TTransformer>();
            //その電柱のコンフィグを取得
            var configParam = block.BlockMasterElement.BlockParam as ElectricPoleBlockParam;
            var range = configParam.MachineConnectionRange;
            
            //その電柱から見て機械が範囲内に存在するか確認
            var poleX = polePos.x;
            var poleY = polePos.y;
            if (poleX - range / 2 > poleX || poleX > poleX + range / 2 || poleY - range / 2 > poleY ||
                poleY > poleY + range / 2) return;
            
            //在る場合は発電機か機械かを判定して接続
            //発電機を電力セグメントに追加
            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            if (worldBlockDatastore.ExistsComponent<TGenerator>(machinePos))
                segment.AddGenerator(worldBlockDatastore.GetBlock<TGenerator>(machinePos));
            else if (worldBlockDatastore.ExistsComponent<TConsumer>(machinePos))
                segment.AddEnergyConsumer(worldBlockDatastore.GetBlock<TConsumer>(machinePos));
        }
    }
}