using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.EnergySystem;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 設置時自動接続の検証結果。対象一覧・使用connectTool・可否をまとめて保持する
    /// Auto-connect evaluation result bundling targets, the chosen connectTool and placeability
    /// </summary>
    public readonly struct ElectricWireAutoConnectPlan
    {
        public readonly IReadOnlyList<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)> Targets;
        public readonly Guid ConnectToolGuid;
        public readonly ElectricWirePlacementFailureReason FailureReason;
        public readonly bool IsPlaceable;

        private ElectricWireAutoConnectPlan(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, Guid connectToolGuid, ElectricWirePlacementFailureReason failureReason, bool isPlaceable)
        {
            Targets = targets;
            ConnectToolGuid = connectToolGuid;
            FailureReason = failureReason;
            IsPlaceable = isPlaceable;
        }

        // ターゲットが0件でも電線不要の正常設置として成功扱いにする
        // Zero targets is still a successful plan; no wire is required
        public static ElectricWireAutoConnectPlan Success(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, Guid connectToolGuid)
        {
            return new ElectricWireAutoConnectPlan(targets, connectToolGuid, ElectricWirePlacementFailureReason.None, true);
        }

        public static ElectricWireAutoConnectPlan Failure(ElectricWirePlacementFailureReason failureReason)
        {
            return new ElectricWireAutoConnectPlan(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), Guid.Empty, failureReason, false);
        }
    }
}
