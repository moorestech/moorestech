using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.EnergySystem
{
    /// <summary>
    /// 電力ワイヤーの端点となるブロックコンポーネント。電柱・機械・発電機すべてが実装を持つ
    /// Block component acting as an electric wire endpoint; poles, machines and generators all carry one
    /// </summary>
    public interface IElectricWireConnector : IBlockComponent
    {
        BlockInstanceId BlockInstanceId { get; }
        bool IsWireConnectionFull { get; }

        // 接続数の上限。0は電線口なし
        // Connection limit; 0 means no wire ports
        int MaxWireConnectionCount { get; }

        // このブロックが持つ電力上の役割。必ず消費・発電・送電のいずれかに紐づく
        // Electric role of this block; always tied to a consumer, generator or transformer
        IElectricEnergyRole EnergyRole { get; }

        IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> WireConnections { get; }

        bool ContainsWireConnection(BlockInstanceId partnerId);
        bool TryAddWireConnection(BlockInstanceId partnerId, ConnectionLineRecord record);
        bool TryRemoveWireConnection(BlockInstanceId partnerId, out ConnectionLineRecord record);
    }
}
