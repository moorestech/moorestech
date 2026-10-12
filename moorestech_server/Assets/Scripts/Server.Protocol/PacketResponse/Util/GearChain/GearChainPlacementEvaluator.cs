using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Construction;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.GearChain
{
    /// <summary>
    /// 歯車チェーンの接続・延長設置可否を判定する共有ロジック。
    /// サーバーの実行処理とクライアントのプレビューが同じ判定を呼ぶことで食い違いを構造的に防ぐ。
    /// Shared judgement logic for gear chain connect and extend placement.
    /// Server execution and client preview call this same judgement to structurally prevent mismatch.
    /// </summary>
    public static class GearChainPlacementEvaluator
    {

        /// <summary>
        /// 距離・既接続・接続数上限・チェーン素材を一括判定する。消費はconnectToolマスタ駆動の複数素材。
        /// reservedMaterials に建設コスト等の予約分を渡すと、同一アイテムの予約数を必要数へ上乗せして判定する。
        /// Evaluate distance, existing connection, connection limit and chain materials at once; consumption is connectTool-master driven multi-material.
        /// Passing reservedMaterials (e.g. construction cost) adds the reserved amount of the same item to the required count.
        /// </summary>
        public static GearChainPlacementJudgement EvaluatePlacement(float connectionDistance, float fromMaxConnectionDistance, float toMaxConnectionDistance, bool alreadyConnected, bool anyConnectionFull, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            var stacks = inventoryItems as IItemStack[] ?? inventoryItems.ToArray();

            var connectionFailure = EvaluateConnection(connectionDistance, fromMaxConnectionDistance, toMaxConnectionDistance,
                alreadyConnected, anyConnectionFull);
            if (connectionFailure != GearChainPlacementFailureReason.None)
                return GearChainPlacementJudgement.Failure(connectionFailure);

            // connectToolマスタから複数素材の必要数を算出する
            // Calculate the required multi-material count from the connectTool master
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, connectionDistance, out var materials)) return GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.NoItem);

            // 予約分を上乗せした必要数を所持が満たすかは共有の正本へ委ねる
            // Whether the held count covers the requirement plus the reservation is delegated to the shared definition
            if (!ConstructionMaterialAccounting.HasEnough(materials, stacks, reservedMaterials)) return GearChainPlacementJudgement.Failure(GearChainPlacementFailureReason.NoItem);

            return GearChainPlacementJudgement.Success(new ConnectionLineRecord(connectToolGuid, materials));
        }
        // 計画時と実行時の接続条件を共有する
        // Share connection conditions between planning and execution
        public static GearChainPlacementFailureReason EvaluateConnection(float distance, float maxA, float maxB,
            bool alreadyConnected, bool anyConnectionFull)
        {
            if (Mathf.Min(maxA, maxB) < distance) return GearChainPlacementFailureReason.TooFar;
            if (alreadyConnected) return GearChainPlacementFailureReason.AlreadyConnected;
            if (anyConnectionFull) return GearChainPlacementFailureReason.ConnectionLimit;
            return GearChainPlacementFailureReason.None;
        }
    }

    /// <summary>
    /// 歯車チェーン設置可否の判定結果
    /// Judgement result of gear chain placement
    /// </summary>
    public readonly struct GearChainPlacementJudgement
    {
        public readonly GearChainPlacementFailureReason FailureReason;
        public readonly ConnectionLineRecord ChainRecord;

        public bool IsPlaceable => FailureReason == GearChainPlacementFailureReason.None;

        private GearChainPlacementJudgement(GearChainPlacementFailureReason failureReason, ConnectionLineRecord chainRecord)
        {
            FailureReason = failureReason;
            ChainRecord = chainRecord;
        }

        public static GearChainPlacementJudgement Success(ConnectionLineRecord chainRecord)
        {
            return new GearChainPlacementJudgement(GearChainPlacementFailureReason.None, chainRecord);
        }

        public static GearChainPlacementJudgement Failure(GearChainPlacementFailureReason reason)
        {
            return new GearChainPlacementJudgement(reason, default);
        }
    }
}
