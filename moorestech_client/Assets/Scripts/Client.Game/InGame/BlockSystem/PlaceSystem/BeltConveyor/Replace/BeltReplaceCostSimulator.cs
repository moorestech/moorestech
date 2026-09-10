using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Common.Debug;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

// 返却品の作り方をクライアントで書き直すと規則が二重管理になるため、サーバーのConstructionCostServiceをそのまま呼ぶ（clientから同Util層を参照する最初の1本）
// Reimplementing how refunds are built on the client would duplicate the rule, so the server's ConstructionCostService is called directly (the first client reference into that Util layer)
using Server.Protocol.PacketResponse.Util.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace
{
    /// <summary>
    /// 張替えを含む列のコストを、サーバーと同じ「セルを1つずつ」の順で先読みする
    /// Looks ahead at the cost of a run containing replace cells, one cell at a time in the server's own order
    ///
    /// サーバーのBeltReplacePlacementServiceは払えないセルで撤去も財布操作も行わないため、列全体の返却を先にプールすると素材不足の境界で判定が反転する
    /// The server's BeltReplacePlacementService touches neither the block nor the wallet on an unpayable cell, so pooling the whole run's refund up front flips the verdict at the shortage boundary
    ///
    /// 見るのはローカルプレイヤーの財布だが、サーバーは撤去時に「設置して支払った人」の財布を引く（ConstructionWalletService.PlanRemoval）。
    /// クライアントは課金元を知りようがないため、自分が置いたベルトを張り替える主用途に合わせた意図的な近似である。
    ///
    /// This reads the local player's wallet, while the server's removal draws on whoever placed and paid for the block (ConstructionWalletService.PlanRemoval).
    /// The client cannot know the payer, so this is a deliberate approximation aimed at the main use case of replacing belts you placed yourself.
    /// </summary>
    public class BeltReplaceCostSimulator
    {
        private static readonly IReadOnlyList<IItemStack> NoRefund = Array.Empty<IItemStack>();
        private static readonly IReadOnlyList<(ItemId itemId, int count)> NoCost = Array.Empty<(ItemId, int)>();

        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly ConstructionWalletQuery _walletQuery;

        // 財布の残りは財布キー（坂は直線代表）で数え、撤去側と設置側の両方がここを進める
        // The remainders are keyed by the wallet block (slopes normalize to the straight one) and both the removal and the placement advance them
        private readonly Dictionary<BlockId, int> _walletRemainders = new();
        private readonly Dictionary<ItemId, int> _availableItemCounts = new();
        private readonly List<IItemStack> _refundedItems = new();

        private BeltReplaceCostSimulator(BlockGameObjectDataStore blockGameObjectDataStore, ConstructionWalletQuery walletQuery)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _walletQuery = walletQuery;
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
                return new BeltReplaceCostSimulator(blockGameObjectDataStore, walletQuery).Simulate(placeInfos, inventoryItems);
            }
            return null;
        }

        private BeltReplaceCostSimulation Simulate(List<PlaceInfo> placeInfos, IEnumerable<IItemStack> inventoryItems)
        {
            foreach (var stack in inventoryItems) AddAvailableItem(stack.Id, stack.Count);

            List<PlaceInfo> unaffordableCells = null;
            foreach (var placeInfo in placeInfos)
            {
                if (!placeInfo.Placeable) continue;
                if (TryPayCell(placeInfo)) continue;

                unaffordableCells ??= new List<PlaceInfo>();
                unaffordableCells.Add(placeInfo);
            }

            // 不足表示は「所持品＋実際に届いた返却品」で見る。拒否されたセルの返却は届かない
            // The shortage display sees the holdings plus the refunds that actually landed; a rejected cell refunds nothing
            var costCheckItems = new List<IItemStack>(inventoryItems);
            costCheckItems.AddRange(_refundedItems);
            return new BeltReplaceCostSimulation(costCheckItems, unaffordableCells);
        }

        // サーバーの1セル分（PlanRemoval→PlanPlacement→CanPayNewCost→2つのCommit）をそのままなぞる
        // Mirrors one server cell: PlanRemoval, PlanPlacement, CanPayNewCost, then the two commits
        private bool TryPayCell(PlaceInfo placeInfo)
        {
            var removal = PlanRemoval(placeInfo);
            var placement = PlanPlacement(placeInfo.BlockId);
            if (!CanPay(placement.ItemsToConsume, removal.RefundItems)) return false;

            CommitRemoval(removal);
            CommitPlacement(placement);
            return true;
        }

        private BeltReplaceRemovalPlan PlanRemoval(PlaceInfo placeInfo)
        {
            if (!placeInfo.IsReplace || !_blockGameObjectDataStore.TryGetBlockGameObject(placeInfo.Position, out var existing)) return new BeltReplaceRemovalPlan(NoRefund, null, false);

            var removedBlockId = existing.BlockId;
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(removedBlockId);
            var walletStatus = _walletQuery.GetWalletStatus(removedBlockId);

            // 財布を通らないブロックは撤去のたびに全額戻る
            // A block that bypasses the wallet refunds its full cost on every removal
            if (!walletStatus.HasValue) return new BeltReplaceRemovalPlan(CreateRefundItems(blockMaster), null, false);

            // 1セット分が貯まる撤去でだけ素材が戻る
            // Materials come back only on the removal that completes one set's worth
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(removedBlockId);
            var condensed = ConstructionWalletUtil.WouldCondense(GetWalletRemainder(walletBlockId, removedBlockId), walletStatus.Value.PlacementsPerCost);
            return new BeltReplaceRemovalPlan(condensed ? CreateRefundItems(blockMaster) : NoRefund, walletBlockId, condensed);
        }

        private BeltReplacePlacementPlan PlanPlacement(BlockId blockId)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var requiredItems = ConstructionCostItems.ToItemCounts(blockMaster.RequiredItems);
            var walletStatus = _walletQuery.GetWalletStatus(blockId);
            if (!walletStatus.HasValue) return new BeltReplacePlacementPlan(requiredItems, null, 0, false);

            // 残りが1つでもあれば素材を払わずに置ける
            // A non-empty remainder covers the cell without paying materials
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(blockId);
            var coveredByWallet = ConstructionWalletUtil.IsCoveredByWallet(GetWalletRemainder(walletBlockId, blockId));
            return new BeltReplacePlacementPlan(coveredByWallet ? NoCost : requiredItems, walletBlockId, walletStatus.Value.PlacementsPerCost, coveredByWallet);
        }

        private bool CanPay(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume, IReadOnlyList<IItemStack> refundItems)
        {
            foreach (var (itemId, count) in itemsToConsume)
            {
                var available = GetAvailableItemCount(itemId);
                foreach (var refundItem in refundItems)
                {
                    if (refundItem.Id == itemId) available += refundItem.Count;
                }
                if (available < count) return false;
            }
            return true;
        }

        private void CommitRemoval(BeltReplaceRemovalPlan removal)
        {
            foreach (var refundItem in removal.RefundItems)
            {
                AddAvailableItem(refundItem.Id, refundItem.Count);
                _refundedItems.Add(refundItem);
            }
            if (!removal.WalletBlockId.HasValue) return;

            // Nに達した分は素材へ凝縮し財布から消える
            // The portion that reached one set's worth condenses into materials and leaves the wallet
            var walletBlockId = removal.WalletBlockId.Value;
            _walletRemainders[walletBlockId] = removal.Condensed ? 0 : GetWalletRemainder(walletBlockId, walletBlockId) + 1;
        }

        private void CommitPlacement(BeltReplacePlacementPlan placement)
        {
            foreach (var (itemId, count) in placement.ItemsToConsume) AddAvailableItem(itemId, -count);
            if (!placement.WalletBlockId.HasValue) return;

            // 素材を払ったセルは1セット分を補充してから1消費する（残り=N-1）
            // A cell that paid materials refills one set's worth and then consumes one (remaining = N-1)
            var walletBlockId = placement.WalletBlockId.Value;
            var remaining = GetWalletRemainder(walletBlockId, walletBlockId);
            _walletRemainders[walletBlockId] = placement.CoveredByWallet ? remaining - 1 : remaining + placement.PlacementsPerCost - 1;
        }

        // 初出の財布はクライアントのミラーが持つ現在値から始める
        // A wallet seen for the first time starts from the current value held by the client mirror
        private int GetWalletRemainder(BlockId walletBlockId, BlockId blockId)
        {
            if (_walletRemainders.TryGetValue(walletBlockId, out var remaining)) return remaining;
            return _walletQuery.GetWalletStatus(blockId)?.RemainingCount ?? 0;
        }

        private int GetAvailableItemCount(ItemId itemId)
        {
            return _availableItemCounts.TryGetValue(itemId, out var count) ? count : 0;
        }

        private void AddAvailableItem(ItemId itemId, int count)
        {
            _availableItemCounts[itemId] = GetAvailableItemCount(itemId) + count;
        }

        private static List<IItemStack> CreateRefundItems(BlockMasterElement blockMaster)
        {
            return ConstructionCostService.CreateRefundItems(ConstructionCostItems.ToItemCounts(blockMaster.RequiredItems));
        }
    }
}
