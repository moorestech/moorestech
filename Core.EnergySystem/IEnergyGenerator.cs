namespace Core.EnergySystem
{
    /// <summary>
    /// 何らかのエネルギーを生産するモノ
    /// </summary>
    public interface IEnergyGenerator
    {
        int OutputEnergy();
        public int EntityId { get;}
    }
}