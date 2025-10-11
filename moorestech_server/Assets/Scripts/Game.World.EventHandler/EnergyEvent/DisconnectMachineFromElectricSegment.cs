using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電力を生産もしくは消費するブロックが削除されたときに、そのブロックをセグメントから削除する
    /// </summary>
    public class DisconnectMachineFromElectricSegment<TSegment, TConsumer, TGenerator>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
    {
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;


        public DisconnectMachineFromElectricSegment(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore)
        {
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
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
            
            if (hasGenerator)
                DisconnectGeneratorFromElectricPole(removedGenerator);
            if (hasConsumer)
                DisconnectConsumerFromElectricPole(removedConsumer);
        }


        /// <summary>
        ///     電柱のセグメントから発電機を削除する
        /// </summary>
        private void DisconnectGeneratorFromElectricPole(TGenerator generator)
        {
            var segment = _worldEnergySegmentDatastore.GetEnergySegment(generator);
            segment.RemoveGenerator(generator);
        }

        /// <summary>
        ///     電柱のセグメントから消費機械を削除する
        /// </summary>
        private void DisconnectConsumerFromElectricPole(TConsumer consumer)
        {
            var segment = _worldEnergySegmentDatastore.GetEnergySegment(consumer);
            segment.RemoveEnergyConsumer(consumer);
        }
    }
}
