using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     エネルギーを消費するモノ
    /// </summary>
    public interface IElectricConsumer : IElectricEnergyRole
    {
        public ElectricPower RequestEnergy { get; }
        void SupplyEnergy(ElectricPower power);
    }
}