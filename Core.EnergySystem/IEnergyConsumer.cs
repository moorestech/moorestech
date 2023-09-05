namespace Core.EnergySystem
{
    /// <summary>
    /// エネルギーを消費するモノ
    /// </summary>
    public interface IEnergyConsumer
    {
        void SupplyEnergy(int power);
        public int EntityId { get; }
        public int RequestEnergy { get; }
    }
}