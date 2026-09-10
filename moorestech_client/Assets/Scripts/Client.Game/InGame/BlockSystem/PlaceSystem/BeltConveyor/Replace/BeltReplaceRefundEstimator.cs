using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace
{
    /// <summary>
    /// 張替えで撤去される既設ブロックの返却素材を見積もる
    /// Estimates the materials refunded by the blocks a replace run removes
    /// サーバーのBeltReplacePlacementService.CanPayNewCostが「所持品＋返却品」で判定するため、プレビューも同じ基準に揃える
    /// The server's BeltReplacePlacementService.CanPayNewCost judges on holdings plus refund, so the preview uses the same basis
    /// </summary>
    public static class BeltReplaceRefundEstimator
    {
        // 実際に撤去されるのは送信対象の張替えセルだけなので、返却の勘定もその条件で集める
        // Only the placeable replace cells are actually removed, so the refund is counted under the same condition
        public static IReadOnlyList<BlockId> CollectReplacedBlockIds(List<PlaceInfo> placeInfos, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            List<BlockId> replacedBlockIds = null;
            foreach (var placeInfo in placeInfos)
            {
                if (!placeInfo.Placeable || !placeInfo.IsReplace) continue;
                if (!blockGameObjectDataStore.TryGetBlockGameObject(placeInfo.Position, out var existing)) continue;

                replacedBlockIds ??= new List<BlockId>();
                replacedBlockIds.Add(existing.BlockId);
            }
            return (IReadOnlyList<BlockId>)replacedBlockIds ?? Array.Empty<BlockId>();
        }

        // 撤去列を先頭から順に財布へ通す。1セット分が貯まる撤去でだけ素材が戻るのはConstructionWalletService.PlanRemovalと同じ規則
        // Runs the removals through the wallet in order; materials come back only on the removal completing one set, as in ConstructionWalletService.PlanRemoval
        public static IEnumerable<IItemStack> AppendRefundItems(IEnumerable<IItemStack> inventoryItems, IReadOnlyList<BlockId> replacedBlockIds, ConstructionWalletQuery walletQuery)
        {
            if (replacedBlockIds.Count == 0) return inventoryItems;

            var availableItems = new List<IItemStack>(inventoryItems);
            var remainingCounts = new Dictionary<BlockId, int>();
            foreach (var replacedBlockId in replacedBlockIds)
            {
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(replacedBlockId);
                var walletStatus = walletQuery.GetWalletStatus(replacedBlockId);

                // 財布を通らないブロックは撤去のたびに全額戻る
                // A block that bypasses the wallet refunds its full cost on every removal
                if (!walletStatus.HasValue)
                {
                    availableItems.AddRange(CreateRefundItems(blockMaster));
                    continue;
                }

                // 坂ベルトは直線と同じ財布を共有するので、残り設置数は財布キーで数える
                // Belt slopes share the straight block's wallet, so the remainder is counted per wallet key
                var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(replacedBlockId);
                if (!remainingCounts.TryGetValue(walletBlockId, out var remaining)) remaining = walletStatus.Value.RemainingCount;

                if (ConstructionWalletUtil.WouldCondense(remaining, walletStatus.Value.PlacementsPerCost))
                {
                    availableItems.AddRange(CreateRefundItems(blockMaster));
                    remaining = 0;
                }
                else
                {
                    remaining++;
                }
                remainingCounts[walletBlockId] = remaining;
            }
            return availableItems;
        }

        private static List<IItemStack> CreateRefundItems(BlockMasterElement blockMaster)
        {
            return ConstructionCostService.CreateRefundItems(ConstructionCostItems.ToItemCounts(blockMaster.RequiredItems));
        }
    }
}
