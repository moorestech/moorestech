using Core.EnergySystem;

namespace Game.World.EventHandler
{
    /// <summary>
    /// エネルギー関連システムのアップデートを行うクラスを束ねるクラス
    /// </summary>
    public class EnergyConnectUpdaterContainer<TSegment,TTransformer,TConsumer,TGenerator> 
        where TSegment : EnergySegment, new()
        where TTransformer : IEnergyTransformer
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
    {
        public EnergyConnectUpdaterContainer()
        {
            new ConnectElectricPoleToElectricSegment();
            new ConnectMachineToElectricSegment<>();
            new DisconnectElectricPoleToFromElectricSegment<>();
        }
    }
}