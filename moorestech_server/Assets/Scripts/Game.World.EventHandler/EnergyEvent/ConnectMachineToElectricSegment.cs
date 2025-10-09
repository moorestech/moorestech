using Core.Master;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using static Game.World.EventHandler.EnergyEvent.EnergyService.ElectricConnectionRangeService;

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
        private readonly int _maxMachineConnectionHorizontalRange;
        private readonly int _maxMachineConnectionHeightRange;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;
        
        
        public ConnectMachineToElectricSegment(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore, MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _maxMachineConnectionHorizontalRange = maxElectricPoleMachineConnectionRange.GetHorizontal();
            _maxMachineConnectionHeightRange = maxElectricPoleMachineConnectionRange.GetHeight();
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockUpdateProperties updateProperties)
        {
            var machinePos = updateProperties.Pos;

            if (!IsElectricMachine(machinePos)) return;

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            foreach (var polePos in EnumerateRange(machinePos, _maxMachineConnectionHorizontalRange, _maxMachineConnectionHeightRange))
            {
                if (!worldBlockDatastore.ExistsComponent<IElectricTransformer>(polePos)) continue;
                ConnectToElectricPole(polePos, machinePos);
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
            var block = worldBlockDatastore.GetBlock(polePos);
            var pole = block.GetComponent<TTransformer>();
            var configParam = (ElectricPoleBlockParam)block.BlockMasterElement.BlockParam;

            if (!IsWithinMachineRange(machinePos, polePos, configParam)) return;

            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            if (worldBlockDatastore.ExistsComponent<TGenerator>(machinePos))
                segment.AddGenerator(worldBlockDatastore.GetBlock<TGenerator>(machinePos));
            else if (worldBlockDatastore.ExistsComponent<TConsumer>(machinePos))
                segment.AddEnergyConsumer(worldBlockDatastore.GetBlock<TConsumer>(machinePos));
        }
    }
}
