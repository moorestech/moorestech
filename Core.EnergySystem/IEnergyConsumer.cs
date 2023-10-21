namespace Core.EnergySystem
{
    /// <summary>
    ///     
    /// </summary>
    public interface IEnergyConsumer
    {
        public int EntityId { get; }
        public int RequestEnergy { get; }
        void SupplyEnergy(int power);
    }
}