using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     エネルギーを消費するモノ。
    ///     電力は個別にプッシュされず、各機械が所属セグメントの確定済み供給率から実効電力を導出する。
    ///     A consumer of electric energy.
    ///     Power is not pushed individually; each machine derives its effective power from its segment's settled supply rate.
    /// </summary>
    public interface IElectricConsumer : IElectricEnergyRole
    {
        public ElectricPower RequestEnergy { get; }
    }
}
