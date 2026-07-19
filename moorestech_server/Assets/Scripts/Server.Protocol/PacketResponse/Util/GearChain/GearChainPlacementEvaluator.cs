using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
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
        public const string TooFarError = "TooFar";
        public const string AlreadyConnectedError = "AlreadyConnected";
        public const string ConnectionLimitError = "ConnectionLimit";
        public const string NoItemError = "NoItem";
        public const string NoPoleItemError = "NoPoleItem";
        public const string InvalidTargetError = "InvalidTarget";
        public const string PositionOccupiedError = "PositionOccupied";
        public const string NotUnlockedError = "NotUnlocked";
        public const string InsufficientItemsError = "InsufficientItems";

        /// <summary>
        /// 距離・既接続・接続数上限・チェーン素材を一括判定する。消費はconnectToolマスタ駆動の複数素材。
        /// reservedMaterials に建設コスト等の予約分を渡すと、同一アイテムの予約数を必要数へ上乗せして判定する。
        /// Evaluate distance, existing connection, connection limit and chain materials at once; consumption is connectTool-master driven multi-material.
        /// Passing reservedMaterials (e.g. construction cost) adds the reserved amount of the same item to the required count.
        /// </summary>
        public static GearChainPlacementJudgement EvaluatePlacement(float connectionDistance, float fromMaxConnectionDistance, float toMaxConnectionDistance, bool alreadyConnected, bool anyConnectionFull, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            var stacks = inventoryItems as IItemStack[] ?? inventoryItems.ToArray();

            // 距離が両端の上限のminを超えると不可
            // Reject when distance exceeds the min of both max distances
            if (Mathf.Min(fromMaxConnectionDistance, toMaxConnectionDistance) < connectionDistance) return GearChainPlacementJudgement.Failure(TooFarError);

            // 既に接続済みの場合は不可
            // Reject when the pair is already connected
            if (alreadyConnected) return GearChainPlacementJudgement.Failure(AlreadyConnectedError);

            // 接続数の上限を確認する
            // Check connection count limit
            if (anyConnectionFull) return GearChainPlacementJudgement.Failure(ConnectionLimitError);

            // connectToolマスタから複数素材の必要数を算出する
            // Calculate the required multi-material count from the connectTool master
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, connectionDistance, out var materials)) return GearChainPlacementJudgement.Failure(NoItemError);

            // 各素材について、予約分を上乗せした必要数を所持が満たすか確認する
            // For each material, verify held count covers the requirement plus any reservation
            foreach (var material in materials)
            {
                var reserved = SumReserved(material.ItemId);
                if (CountItem(material.ItemId) < material.Count + reserved) return GearChainPlacementJudgement.Failure(NoItemError);
            }

            return GearChainPlacementJudgement.Success(new GearChainConnectionCost(materials));

            #region Internal

            int SumReserved(ItemId itemId)
            {
                // 予約リスト中の同一アイテム数を合計する
                // Sum the reserved amount of the same item in the reservation list
                if (reservedMaterials == null) return 0;
                var reserved = 0;
                foreach (var material in reservedMaterials)
                {
                    if (material.ItemId == itemId) reserved += material.Count;
                }
                return reserved;
            }

            int CountItem(ItemId itemId)
            {
                // 対象アイテムの合計所持数を数える
                // Count total owned amount of the item
                var total = 0;
                foreach (var stack in stacks)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }

                return total;
            }

            #endregion
        }
    }

    /// <summary>
    /// 歯車チェーン設置可否の判定結果。失敗理由またはチェーン消費コストを保持する
    /// Judgement result of gear chain placement, holding failure reason or chain consumption cost
    /// </summary>
    public readonly struct GearChainPlacementJudgement
    {
        public readonly string FailureReason;
        public readonly GearChainConnectionCost ChainCost;

        public bool IsPlaceable => string.IsNullOrEmpty(FailureReason);

        private GearChainPlacementJudgement(string failureReason, GearChainConnectionCost chainCost)
        {
            FailureReason = failureReason;
            ChainCost = chainCost;
        }

        public static GearChainPlacementJudgement Success(GearChainConnectionCost chainCost)
        {
            return new GearChainPlacementJudgement(string.Empty, chainCost);
        }

        public static GearChainPlacementJudgement Failure(string reason)
        {
            return new GearChainPlacementJudgement(reason, default);
        }
    }
}
