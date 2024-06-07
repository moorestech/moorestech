using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     エネルギーを伝達するモノ
    /// </summary>
    public interface IElectricTransformer : IBlockComponent
    {
        public BlockInstanceId BlockInstanceId { get; }
    }
}