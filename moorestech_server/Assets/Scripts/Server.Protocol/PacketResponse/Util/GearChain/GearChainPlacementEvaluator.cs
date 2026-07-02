using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
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

        /// <summary>
        /// 距離・既接続・接続数上限・チェーンアイテム・ポールアイテムを一括判定する。
        /// poleItemId が ItemMaster.EmptyItemId の場合はポールアイテムの所持チェックを行わない（既存ポール同士の接続用）。
        /// Evaluate distance, existing connection, connection limit, chain items and pole item at once.
        /// When poleItemId is ItemMaster.EmptyItemId, the pole item check is skipped (for pole-to-pole connection).
        /// </summary>
        public static GearChainPlacementJudgement EvaluatePlacement(float connectionDistance, float fromMaxConnectionDistance, float toMaxConnectionDistance, bool alreadyConnected, bool anyConnectionFull, ItemId chainItemId, IEnumerable<IItemStack> inventoryItems, ItemId poleItemId)
        {
            var stacks = inventoryItems as IItemStack[] ?? inventoryItems.ToArray();

            // 距離が両端の上限のminを超えると不可
            // Reject when distance exceeds the min of both max distances
            if (connectionDistance > Mathf.Min(fromMaxConnectionDistance, toMaxConnectionDistance)) return GearChainPlacementJudgement.Failure(TooFarError);

            // 既に接続済みの場合は不可
            // Reject when the pair is already connected
            if (alreadyConnected) return GearChainPlacementJudgement.Failure(AlreadyConnectedError);

            // 接続数の上限を確認する
            // Check connection count limit
            if (anyConnectionFull) return GearChainPlacementJudgement.Failure(ConnectionLimitError);

            // チェーンアイテムの必要数を所持しているか確認する
            // Ensure required chain items are owned
            if (!TryCalculateChainCost(chainItemId, connectionDistance, out var chainCost)) return GearChainPlacementJudgement.Failure(NoItemError);
            if (CountItem(stacks, chainItemId) < chainCost.Count) return GearChainPlacementJudgement.Failure(NoItemError);

            // 延長設置時はポールアイテムの所持を確認する
            // Ensure a pole item is owned when extending
            if (poleItemId != ItemMaster.EmptyItemId && CountItem(stacks, poleItemId) < 1) return GearChainPlacementJudgement.Failure(NoPoleItemError);

            return GearChainPlacementJudgement.Success(chainCost);
        }

        /// <summary>
        /// チェーンアイテム設定から距離に応じた消費数を算出する
        /// Calculate consumption count for the distance from chain item master
        /// </summary>
        public static bool TryCalculateChainCost(ItemId chainItemId, float distance, out GearChainConnectionCost chainCost)
        {
            chainCost = default;

            // 指定アイテムがチェーンアイテム設定に含まれるか確認する
            // Check the specified item exists in the chain item master
            foreach (var gearChainItem in MasterHolder.BlockMaster.Blocks.GearChainItems)
            {
                if (MasterHolder.ItemMaster.GetItemId(gearChainItem.ItemGuid) != chainItemId) continue;

                var required = Mathf.CeilToInt(distance / gearChainItem.ConsumptionPerLength);
                chainCost = new GearChainConnectionCost(chainItemId, required);
                return true;
            }

            return false;
        }

        private static int CountItem(IItemStack[] stacks, ItemId itemId)
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
