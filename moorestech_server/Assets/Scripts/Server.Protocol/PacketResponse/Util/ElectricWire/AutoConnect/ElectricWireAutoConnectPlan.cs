using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.EnergySystem;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 設置時自動接続の検証結果。対象一覧・使用connectTool・可否・素材消費の有無をまとめて保持する
    /// Auto-connect evaluation result bundling targets, the chosen connectTool, placeability and whether materials are consumed
    /// </summary>
    public readonly struct ElectricWireAutoConnectPlan
    {
        public readonly IReadOnlyList<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)> Targets;
        public readonly Guid ConnectToolGuid;
        public readonly ElectricWirePlacementFailureReason FailureReason;
        public readonly bool IsPlaceable;
        // 無料設置デバッグでは接続だけ行い素材を消費しない（ADR 0056）
        // The free-placement debug only connects and never consumes materials (ADR 0056)
        public readonly bool ConsumesMaterials;

        private ElectricWireAutoConnectPlan(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, Guid connectToolGuid, ElectricWirePlacementFailureReason failureReason, bool isPlaceable, bool consumesMaterials)
        {
            Targets = targets;
            ConnectToolGuid = connectToolGuid;
            FailureReason = failureReason;
            IsPlaceable = isPlaceable;
            ConsumesMaterials = consumesMaterials;
        }

        // ターゲットが0件でも電線不要の正常設置として成功扱いにする
        // Zero targets is still a successful plan; no wire is required
        public static ElectricWireAutoConnectPlan Success(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, Guid connectToolGuid, bool consumesMaterials)
        {
            return new ElectricWireAutoConnectPlan(targets, connectToolGuid, ElectricWirePlacementFailureReason.None, true, consumesMaterials);
        }

        public static ElectricWireAutoConnectPlan Failure(ElectricWirePlacementFailureReason failureReason)
        {
            return new ElectricWireAutoConnectPlan(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), Guid.Empty, failureReason, false, false);
        }
    }
}
