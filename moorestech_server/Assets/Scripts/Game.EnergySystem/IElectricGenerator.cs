using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     何らかのエネルギーを生産するモノ
    /// </summary>
    public interface IElectricGenerator : IBlockComponent
    {
        public BlockInstanceId BlockInstanceId { get; }
        ElectricPower OutputEnergy();
    }
}