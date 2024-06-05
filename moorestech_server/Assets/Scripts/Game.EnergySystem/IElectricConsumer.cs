using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     エネルギーを消費するモノ
    /// </summary>
    public interface IElectricConsumer : IBlockComponent
    {
        public EntityID EntityId { get; }
        public int RequestEnergy { get; }
        void SupplyEnergy(int power);
    }
}