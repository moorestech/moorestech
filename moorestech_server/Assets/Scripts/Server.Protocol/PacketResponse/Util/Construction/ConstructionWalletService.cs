using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 残り設置数の財布。設置・撤去とも「何を消費/返却するか」の指示だけを返し、判断を内側に閉じる
    /// The remaining-placement wallet; placement and removal both hand back an instruction saying what to consume or refund, keeping every decision inside
    /// </summary>
    public class ConstructionWalletService
    {
        private readonly IRemainingPlacementCountLookup _lookup;
        private readonly IRemainingPlacementCountMutation _mutation;
        private readonly ConstructionPayerDataStore _payers;

        public ConstructionWalletService(IRemainingPlacementCountLookup lookup, IRemainingPlacementCountMutation mutation, ConstructionPayerDataStore payers)
        {
            _lookup = lookup;
            _mutation = mutation;
            _payers = payers;
        }

        // 問い合わせ後、確定でCommitPlacementを呼ぶ
        // Ask, then call CommitPlacement once final
        public IConstructionPlacementPlan PlanPlacement(BlockMasterElement blockMaster, int playerId)
        {
            if (!ConstructionWalletUtil.UsesWallet(blockMaster.PlacementsPerCost)) return new DirectCostPlacementPlan(ConstructionCostItems.ToItemCounts(blockMaster.RequiredItems));

            // 消費素材と賄えるかの判断は共有の問い合わせ窓口に任せる
            // What to consume and whether the remainder covers it are both decided by the shared query window
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
            var query = new ConstructionWalletQuery(_lookup.GetReader(playerId));
            var usage = query.IsCoveredByWallet(blockId) ? ConstructionWalletUsage.CoveredByWallet : ConstructionWalletUsage.PaidAndRefilled;
            return new WalletPlacementPlan(query.GetItemsToConsume(blockId), _mutation, _payers, usage, playerId, ConstructionWalletUtil.ResolveWalletBlockId(blockId), blockMaster.PlacementsPerCost);
        }

        public void CommitPlacement(IConstructionPlacementPlan plan, IOpenableInventory inventory, BlockInstanceId blockInstanceId)
        {
            plan.Commit(inventory, blockInstanceId);
        }

        // 問い合わせ後、確定でCommitRemovalを呼ぶ
        // Ask, then call CommitRemoval once final
        public IConstructionRemovalPlan PlanRemoval(BlockMasterElement blockMaster, BlockInstanceId blockInstanceId, int removePlayerId)
        {
            var fullCost = ConstructionCostItems.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionWalletUtil.UsesWallet(blockMaster.PlacementsPerCost)) return new DirectCostRemovalPlan(ConstructionCostService.CreateRefundItems(fullCost));

            // 戻し先は撤去した人ではなく設置して支払った人の財布
            // The remainder goes back to whoever placed and paid for the block, not to whoever removes it
            var payerPlayerId = _payers.GetPayer(blockInstanceId, removePlayerId);

            // 1セット分が貯まる撤去でだけ素材が戻る
            // Materials come back only on the removal that completes one set's worth
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            var condensed = ConstructionWalletUtil.WouldCondense(_lookup.GetRemainingCount(payerPlayerId, walletBlockId), blockMaster.PlacementsPerCost);
            IReadOnlyList<IItemStack> refund = condensed ? ConstructionCostService.CreateRefundItems(fullCost) : Array.Empty<IItemStack>();
            return new WalletRemovalPlan(refund, _mutation, _payers, payerPlayerId, walletBlockId, blockInstanceId, condensed);
        }

        public void CommitRemoval(IConstructionRemovalPlan plan)
        {
            plan.Commit();
        }

        // 設置・撤去1操作の末尾で呼び、溜まった残り設置数の変更を財布ごと1通へ集約する
        // Called at the end of one place/remove operation to collapse the accumulated changes into one notification per wallet
        public void FlushRemainingCountChanges()
        {
            _mutation.FlushChanges();
        }
    }
}
