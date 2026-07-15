using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    ///     電力上の役割（消費・発電・送電）を束ねる共通コンポーネントインターフェース。
    ///     ワイヤー端点は必ずいずれかの役割に紐づくため、役割なしの端点は型レベルで存在しない。
    ///     Common component interface bundling the electric roles: consumer, generator and transformer.
    ///     Every wire endpoint is always tied to one of these roles, so a roleless endpoint cannot exist by type.
    /// </summary>
    public interface IElectricEnergyRole : IBlockComponent
    {
        public BlockInstanceId BlockInstanceId { get; }
    }
}
