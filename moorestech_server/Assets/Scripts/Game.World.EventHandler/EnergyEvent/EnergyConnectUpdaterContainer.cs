using Game.EnergySystem;
using Game.World.EventHandler.EnergyEvent.EnergyService;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.EnergyEvent
{
    /// <summary>
    ///     電柱や機械が設置されたときに、セグメントへの接続、切断を行うイベントクラスをまとめたクラス
    /// </summary>
    public class EnergyConnectUpdaterContainer<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
        where TTransformer : IElectricTransformer
    {
        public EnergyConnectUpdaterContainer(IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore, MaxElectricPoleMachineConnectionRange maxElectricPoleMachineConnectionRange)
        {
            new ConnectElectricPoleToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>(worldEnergySegmentDatastore);
            new ConnectMachineToElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>(worldEnergySegmentDatastore, maxElectricPoleMachineConnectionRange);

            new DisconnectElectricPoleToFromElectricSegment<TSegment, TConsumer, TGenerator, TTransformer>(worldEnergySegmentDatastore);
            new DisconnectMachineFromElectricSegment<TSegment, TConsumer, TGenerator>(worldEnergySegmentDatastore);
        }
    }
}