using Game.Block.Interface.Component;

namespace Core.EnergySystem
{
    /// <summary>
    ///     エネルギーを消費するモノ
    /// </summary>
    public interface IElectricConsumer : IBlockComponent
    {
        public int EntityId { get; }
        public int RequestEnergy { get; }
        void SupplyEnergy(int power);
    }
}