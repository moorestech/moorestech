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
        public const string InvalidTargetError = "InvalidTarget";
        public const string PositionOccupiedError = "PositionOccupied";
        public const string NotUnlockedError = "NotUnlocked";
        public const string InsufficientItemsError = "InsufficientItems";

        /// <summary>
        /// 距離・既接続・接続数上限・チェーンアイテムを一括判定する。
        /// reservedItemCounts に建設コスト等の予約分を渡すと、チェーンと同一アイテムの予約数を必要数へ上乗せして判定する。
        /// Evaluate distance, existing connection, connection limit and chain items at once.
        /// Passing reservedItemCounts (e.g. construction cost) adds the reserved amount of the same item as the chain to the required count.
        /// </summary>
        public static GearChainPlacementJudgement EvaluatePlacement(float connectionDistance, float fromMaxConnectionDistance, float toMaxConnectionDistance, bool alreadyConnected, bool anyConnectionFull, ItemId chainItemId, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<(ItemId itemId, int count)> reservedItemCounts)
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

            // チェーンアイテムの必要数を算出する
            // Calculate the required chain item count
            if (!TryCalculateChainCost(chainItemId, connectionDistance, out var chainCost)) return GearChainPlacementJudgement.Failure(NoItemError);

            // 予約リスト中のチェーンと同一アイテム分を必要数へ上乗せする
            // Add the same-item amount reserved in the list on top of the required chain count
            var reservedChain = 0;
            if (reservedItemCounts != null)
            {
                foreach (var (itemId, count) in reservedItemCounts)
                {
                    if (itemId == chainItemId) reservedChain += count;
                }
            }
            if (CountItem(chainItemId) < chainCost.Count + reservedChain) return GearChainPlacementJudgement.Failure(NoItemError);

            return GearChainPlacementJudgement.Success(chainCost);

            #region Internal

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

        /// <summary>
        /// チェーンアイテム設定から距離に応じた消費数を算出する
        /// Calculate consumption count for the distance from chain item master
        /// </summary>
        private static bool TryCalculateChainCost(ItemId chainItemId, float distance, out GearChainConnectionCost chainCost)
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
