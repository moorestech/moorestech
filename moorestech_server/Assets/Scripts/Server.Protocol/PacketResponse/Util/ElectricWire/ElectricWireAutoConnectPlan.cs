using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.EnergySystem;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// 設置時自動接続の検証結果。対象一覧・コスト・電線アイテム・可否をまとめて保持する
    /// Auto-connect evaluation result bundling targets, cost, wire item and placeability
    /// </summary>
    public readonly struct ElectricWireAutoConnectPlan
    {
        public readonly IReadOnlyList<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)> Targets;
        public readonly ItemId WireItemId;
        public readonly string FailureReason;
        public readonly bool IsPlaceable;

        private ElectricWireAutoConnectPlan(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, ItemId wireItemId, string failureReason, bool isPlaceable)
        {
            Targets = targets;
            WireItemId = wireItemId;
            FailureReason = failureReason;
            IsPlaceable = isPlaceable;
        }

        // ターゲットが0件でも電線不要の正常設置として成功扱いにする
        // Zero targets is still a successful plan; no wire is required
        public static ElectricWireAutoConnectPlan Success(IReadOnlyList<(BlockInstanceId, ElectricWireConnectionCost)> targets, ItemId wireItemId)
        {
            return new ElectricWireAutoConnectPlan(targets, wireItemId, string.Empty, true);
        }

        public static ElectricWireAutoConnectPlan Failure(string failureReason)
        {
            return new ElectricWireAutoConnectPlan(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), ItemMaster.EmptyItemId, failureReason, false);
        }
    }
}
