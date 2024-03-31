using Server.Core.EnergySystem;
using Game.Block.Interface.BlockConfig;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電柱や機械が設置されたときに、セグメントへの接続、切断を行うイベントクラスをまとめたクラス
    /// </summary>
    public class EnergyConnectUpdaterContainer<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer
    {
        public EnergyConnectUpdaterContainer(
            IBlockRemoveEvent blockRemoveEvent,
            IBlockConfig blockConfig,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore,
            MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange,
            IWorldBlockDatastore worldBlockDatastore,
            IBlockPlaceEvent blockPlaceEvent)
        {
            new ConnectElectricPoleToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>(blockPlaceEvent,
                worldEnergySegmentDatastore, blockConfig, worldBlockDatastore);
            new ConnectMachineToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>(blockPlaceEvent,
                worldEnergySegmentDatastore, blockConfig, maxElectricPoleMachineConnectionRange, worldBlockDatastore);

            new DisconnectElectricPoleToFromElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>(
                blockRemoveEvent, blockConfig, worldEnergySegmentDatastore, worldBlockDatastore);
        }
    }
}