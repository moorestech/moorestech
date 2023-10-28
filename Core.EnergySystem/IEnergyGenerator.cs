namespace Core.EnergySystem
{
    /// <summary>
    ///     何らかのエネルギーを生産するモノ
    /// </summary>
    public interface IEnergyGenerator
    {
        public int EntityId { get; }
        int OutputEnergy();
    }
}