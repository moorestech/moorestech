using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     エネルギーを消費するモノ
    /// </summary>
    public interface IElectricConsumer : IBlockComponent
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy { get; }
        void SupplyEnergy(ElectricPower power);
    }
}