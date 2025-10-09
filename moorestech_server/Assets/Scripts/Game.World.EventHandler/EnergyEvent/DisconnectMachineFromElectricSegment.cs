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

            // 発電機または消費機械かどうか判定
            var isGenerator = worldBlockDatastore.TryGetBlock<TGenerator>(machinePos, out var removedGenerator);
            var isConsumer = worldBlockDatastore.TryGetBlock<TConsumer>(machinePos, out var removedConsumer);

            if (!isGenerator && !isConsumer) return;

            foreach (var polePos in EnumerateRange(machinePos, _maxMachineConnectionHorizontalRange, _maxMachineConnectionHeightRange))
            {
                if (!worldBlockDatastore.ExistsComponent<IElectricTransformer>(polePos)) continue;

                if (isGenerator)
                    DisconnectGeneratorFromElectricPole(polePos, machinePos, removedGenerator);
                if (isConsumer)
                    DisconnectConsumerFromElectricPole(polePos, machinePos, removedConsumer);
            }
        }


        /// <summary>
        ///     電柱のセグメントから発電機を削除する
        /// </summary>
        private void DisconnectGeneratorFromElectricPole(Vector3Int polePos, Vector3Int machinePos, TGenerator generator)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //電柱を取得
            var block = worldBlockDatastore.GetBlock(polePos);
            var pole = block.GetComponent<TTransformer>();
            var configParam = (ElectricPoleBlockParam)block.BlockMasterElement.BlockParam;

            if (!IsWithinMachineRange(machinePos, polePos, configParam)) return;

            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            segment.RemoveGenerator(generator);
        }

        /// <summary>
        ///     電柱のセグメントから消費機械を削除する
        /// </summary>
        private void DisconnectConsumerFromElectricPole(Vector3Int polePos, Vector3Int machinePos, TConsumer consumer)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            //電柱を取得
            var block = worldBlockDatastore.GetBlock(polePos);
            var pole = block.GetComponent<TTransformer>();
            var configParam = (ElectricPoleBlockParam)block.BlockMasterElement.BlockParam;

            if (!IsWithinMachineRange(machinePos, polePos, configParam)) return;

            var segment = _worldEnergySegmentDatastore.GetEnergySegment(pole);
            segment.RemoveEnergyConsumer(consumer);
        }
    }
}
