namespace Core.EnergySystem
{
    /// <summary>
    ///     
    /// </summary>
    public interface IEnergyGenerator
    {
        public int EntityId { get; }
        int OutputEnergy();
    }
}