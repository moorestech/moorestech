using Game.Block.Interface;
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
    ///     電力を生産もしくは消費するブロックが削除されたときに、そのブロックをセグメントから削除する
    /// </summary>
    public class DisconnectMachineFromElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
        where TTransformer : IElectricTransformer
    {
        private readonly int _maxMachineConnectionHorizontalRange;
        private readonly int _maxMachineConnectionHeightRange;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;


        public DisconnectMachineFromElectricSegment(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore, MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _maxMachineConnectionHorizontalRange = maxElectricPoleMachineConnectionRange.GetHorizontal();
            _maxMachineConnectionHeightRange = maxElectricPoleMachineConnectionRange.GetHeight();
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }

        private void OnBlockRemove(BlockUpdateProperties updateProperties)
        {
            var machinePos = updateProperties.Pos;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var machineBlock = worldBlockDatastore.GetBlock(machinePos);

            if (machineBlock == null) return;

            var hasGenerator = machineBlock.ComponentManager.TryGetComponent(out TGenerator removedGenerator);
            var hasConsumer = machineBlock.ComponentManager.TryGetComponent(out TConsumer removedConsumer);

            if (!hasGenerator && !hasConsumer) return;

            foreach (var polePos in EnumerateCandidatePolePositions(machineBlock.BlockPositionInfo, _maxMachineConnectionHorizontalRange, _maxMachineConnectionHeightRange))
            {
                if (!worldBlockDatastore.ExistsComponent<IElectricTransformer>(polePos)) continue;

                if (hasGenerator)
                    DisconnectGeneratorFromElectricPole(polePos, machineBlock.BlockPositionInfo, removedGenerator);
                if (hasConsumer)
                    DisconnectConsumerFromElectricPole(polePos, machineBlock.BlockPositionInfo, removedConsumer);
            }
        }


        /// <summary>
        ///     電柱のセグメントから発電機を削除する
        /// </summary>
        private void DisconnectGeneratorFromElectricPole(Vector3Int polePos, BlockPositionInfo machineInfo, TGenerator generator)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //電柱を取得
            var block = worldBlockDatastore.GetBlock(polePos);
            var pole = block.GetComponent<TTransformer>();
            var configParam = (ElectricPoleBlockParam)block.BlockMasterElement.BlockParam;

            if (!IsWithinMachineRange(machineInfo, polePos, configParam)) return;

            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            segment.RemoveGenerator(generator);
        }

        /// <summary>
        ///     電柱のセグメントから消費機械を削除する
        /// </summary>
        private void DisconnectConsumerFromElectricPole(Vector3Int polePos, BlockPositionInfo machineInfo, TConsumer consumer)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //電柱を取得
            var block = worldBlockDatastore.GetBlock(polePos);
            var pole = block.GetComponent<TTransformer>();
            var configParam = (ElectricPoleBlockParam)block.BlockMasterElement.BlockParam;

            if (!IsWithinMachineRange(machineInfo, polePos, configParam)) return;

            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            segment.RemoveEnergyConsumer(consumer);
        }
    }
}
