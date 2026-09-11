using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost
{
    /// <summary>
    /// 張替えを含む列のコストを、サーバーと同じ「セルを1つずつ」の順で先読みする
    /// Looks ahead at the cost of a run containing replace cells, one cell at a time in the server's own order
    ///
    /// サーバーのBeltReplacePlacementServiceは払えないセルで撤去も財布操作も行わないため、列全体の返却を先にプールすると素材不足の境界で判定が反転する
    /// The server's BeltReplacePlacementService touches neither the block nor the wallet on an unpayable cell, so pooling the whole run's refund up front flips the verdict at the shortage boundary
    ///
    /// 財布残高の遷移式と費用の充足判定はサーバーと同じConstructionWalletUtil・ConstructionCostRulesを呼ぶ。ここ固有なのはセル走査順だけ
    /// The wallet transitions and the affordability judgement call the very ConstructionWalletUtil and ConstructionCostRules the server calls; the only logic of its own here is the cell scan order
    ///
    /// 撤去の返却は「設置して支払った人」の財布で決まる（ConstructionWalletService.PlanRemoval）。クライアントは課金元を知りようがないため、
    /// 自分の財布では払えないセルも全額返却を見込めば払えるならPlaceableのまま送り、サーバーへ最終判定を預ける
    ///
    /// A removal's refund is decided by whoever placed and paid for the block (ConstructionWalletService.PlanRemoval), and the client cannot know the payer,
    /// so a cell unpayable against the player's own wallet stays Placeable and is sent whenever a full refund would cover it, leaving the final call to the server
    /// </summary>
    public class BeltReplaceCostSimulator
    {
        private static readonly IReadOnlyList<IItemStack> NoRefund = Array.Empty<IItemStack>();
        private static readonly IReadOnlyList<(ItemId itemId, int count)> NoCost = Array.Empty<(ItemId, int)>();

        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly ConstructionWalletQuery _walletQuery;
        private readonly BeltReplaceCostLedger _ledger;

        // 一度でも課金元不明の返却を見込むと、以降のセルの残高も所持数も仮定の上に乗る
        // Once a refund with an unknown payer is assumed, every later cell's remainder and holdings rest on that assumption
        private bool _refundAssumed;

        private BeltReplaceCostSimulator(BlockGameObjectDataStore blockGameObjectDataStore, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _walletQuery = walletQuery;
            _ledger = new BeltReplaceCostLedger(walletQuery, inventoryItems);
        }

        // 張替えセルを含まない列はnullを返し、通常設置と同じバッチ判定へ委ねる
        // A run without replace cells returns null and is left to the same batch check as normal placement
        public static BeltReplaceCostSimulation TrySimulate(List<PlaceInfo> placeInfos, BlockGameObjectDataStore blockGameObjectDataStore, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            // デバッグ中はコスト判定をスキップ
            // Skip cost checks during debug placement
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return null;

            foreach (var placeInfo in placeInfos)
            {
                if (!placeInfo.Placeable || !placeInfo.IsReplace) continue;
                return new BeltReplaceCostSimulator(blockGameObjectDataStore, walletQuery, inventoryItems).Simulate(placeInfos, inventoryItems);
            }
            return null;
        }

        private BeltReplaceCostSimulation Simulate(List<PlaceInfo> placeInfos, IEnumerable<IItemStack> inventoryItems)
        {
            List<PlaceInfo> unaffordableCells = null;
            List<int> uncertainCellIndices = null;
            for (var i = 0; i < placeInfos.Count; i++)
            {
                var placeInfo = placeInfos[i];
                if (!placeInfo.Placeable) continue;

                if (PayCell(placeInfo) == BeltReplaceCellPayment.Unaffordable)
                {
                    unaffordableCells ??= new List<PlaceInfo>();
                    unaffordableCells.Add(placeInfo);
                    continue;
                }

                // 仮定を使った後のセルは、払えていても課金元次第で結果が変わる
                // Once the assumption is in play, even a paid cell's outcome still depends on the payer
                if (!_refundAssumed) continue;
                uncertainCellIndices ??= new List<int>();
                uncertainCellIndices.Add(i);
            }

            // 不足表示は「所持品＋実際に届いた返却品」で見る。拒否されたセルの返却は届かない
            // The shortage display sees the holdings plus the refunds that actually landed; a rejected cell refunds nothing
            var costCheckItems = new List<IItemStack>(inventoryItems);
            costCheckItems.AddRange(_ledger.ArrivedRefunds);
            return new BeltReplaceCostSimulation(costCheckItems, unaffordableCells, uncertainCellIndices);
        }

        // サーバーの1セル分（PlanRemoval→PlanPlacement→CanPayNewCost→2つのCommit）をそのままなぞる
        // Mirrors one server cell: PlanRemoval, PlanPlacement, CanPayNewCost, then the two commits
        private BeltReplaceCellPayment PayCell(PlaceInfo placeInfo)
        {
            var removal = PlanRemoval();
            var placement = PlanPlacement(placeInfo.BlockId);
            if (_ledger.CanPay(placement.ItemsToConsume, removal.RefundItems))
            {
                removal.Commit(_ledger);
                placement.Commit(_ledger);
                return BeltReplaceCellPayment.Paid;
            }

            // 自分の財布では払えないが、課金元が別人なら全額返却が来て払えるかもしれない。成功し得る操作を送信前に潰さない
            // The player's own wallet cannot pay, yet a different payer's wallet may hand back the full cost; an operation that could succeed is never dropped before sending
            var assumedRemoval = PlanAssumedFullRefundRemoval();
            if (assumedRemoval == null || !_ledger.CanPay(placement.ItemsToConsume, assumedRemoval.RefundItems)) return BeltReplaceCellPayment.Unaffordable;

            _refundAssumed = true;
            assumedRemoval.Commit(_ledger);
            placement.Commit(_ledger);
            return BeltReplaceCellPayment.PaidWithAssumedRefund;

            #region Internal

            IBeltReplaceRemovalPlan PlanRemoval()
            {
                if (!TryResolveRemovedBlock(out var removedBlockId, out var walletStatus)) return new DirectCostBeltReplaceRemovalPlan(NoRefund);

                // 財布を通らないブロックは撤去のたびに全額戻る
                // A block that bypasses the wallet refunds its full cost on every removal
                if (!walletStatus.HasValue) return new DirectCostBeltReplaceRemovalPlan(CreateRefundItems(removedBlockId));

                // 1セット分が貯まる撤去でだけ素材が戻る
                // Materials come back only on the removal that completes one set's worth
                var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(removedBlockId);
                var condensed = ConstructionWalletUtil.WouldCondense(_ledger.GetWalletRemainder(walletBlockId), walletStatus.Value.PlacementsPerCost);
                return new WalletBeltReplaceRemovalPlan(condensed ? CreateRefundItems(removedBlockId) : NoRefund, walletBlockId, condensed);
            }

            // 全額返却を見込んだ代替案。自分の財布の見積りと同じ結論になるなら不確実さは無いのでnull
            // The alternative assuming a full refund; null when it would reach the same conclusion as the player's own estimate, since then nothing is uncertain
            AssumedFullRefundBeltReplaceRemovalPlan PlanAssumedFullRefundRemoval()
            {
                if (!TryResolveRemovedBlock(out var removedBlockId, out var walletStatus) || !walletStatus.HasValue) return null;

                var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(removedBlockId);
                if (ConstructionWalletUtil.WouldCondense(_ledger.GetWalletRemainder(walletBlockId), walletStatus.Value.PlacementsPerCost)) return null;

                return new AssumedFullRefundBeltReplaceRemovalPlan(CreateRefundItems(removedBlockId), walletBlockId);
            }

            bool TryResolveRemovedBlock(out BlockId removedBlockId, out ConstructionWalletStatus? walletStatus)
            {
                removedBlockId = default;
                walletStatus = null;
                if (!placeInfo.IsReplace || !_blockGameObjectDataStore.TryGetBlockGameObject(placeInfo.Position, out var existing)) return false;

                removedBlockId = existing.BlockId;
                walletStatus = _walletQuery.GetWalletStatus(removedBlockId);
                return true;
            }

            IBeltReplacePlacementPlan PlanPlacement(BlockId blockId)
            {
                var requiredItems = ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems);
                var walletStatus = _walletQuery.GetWalletStatus(blockId);
                if (!walletStatus.HasValue) return new DirectCostBeltReplacePlacementPlan(requiredItems);

                // 残りが1つでもあれば素材を払わずに置ける
                // A non-empty remainder covers the cell without paying materials
                var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(blockId);
                var coveredByWallet = ConstructionWalletUtil.IsCoveredByWallet(_ledger.GetWalletRemainder(walletBlockId));
                return new WalletBeltReplacePlacementPlan(coveredByWallet ? NoCost : requiredItems, walletBlockId, walletStatus.Value.PlacementsPerCost, coveredByWallet);
            }

            List<IItemStack> CreateRefundItems(BlockId removedBlockId)
            {
                return ConstructionCostRules.CreateRefundItems(ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(removedBlockId).RequiredItems));
            }

            #endregion
        }
    }
}
