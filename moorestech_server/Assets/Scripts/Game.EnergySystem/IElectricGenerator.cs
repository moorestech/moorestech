using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     何らかのエネルギーを生産するモノ
    /// </summary>
    public interface IElectricGenerator : IBlockComponent
    {
        public int EntityId { get; }
        int OutputEnergy();
    }
}