using Game.Block.Interface.Component;

namespace Game.Gear.Common
{
    /// <summary>
    /// ギアコネクタのインターフェース
    /// Interface for gear connectors
    /// </summary>
    /// <remarks>
    /// IBlockConnectorにギア固有のオプションを追加
    /// Adds gear-specific options to IBlockConnector
    /// </remarks>
    public interface IGearConnector : IBlockConnector
    {
        IGearConnectOption Option { get; }
    }
}
