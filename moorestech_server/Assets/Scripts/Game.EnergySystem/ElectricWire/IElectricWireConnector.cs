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
        float MaxWireLength { get; }
        bool IsWireConnectionFull { get; }

        // このブロックが持つ電力上の役割。必ず消費・発電・送電のいずれかに紐づく
        // Electric role of this block; always tied to a consumer, generator or transformer
        IElectricEnergyRole EnergyRole { get; }

        IReadOnlyDictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionCost Cost)> WireConnections { get; }

        bool ContainsWireConnection(BlockInstanceId partnerId);
        bool TryAddWireConnection(BlockInstanceId partnerId, ElectricWireConnectionCost cost);
        bool TryRemoveWireConnection(BlockInstanceId partnerId, out ElectricWireConnectionCost cost);
    }
}
