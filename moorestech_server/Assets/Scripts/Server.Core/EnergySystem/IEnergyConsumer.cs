namespace Server.Core.EnergySystem
{
    /// <summary>
    ///     エネルギーを消費するモノ
    /// </summary>
    public interface IEnergyConsumer
    {
        public int EntityId { get; }
        public int RequestEnergy { get; }
        void SupplyEnergy(int power);
    }
}