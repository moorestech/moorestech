using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;

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

        // プレイヤーごとの問い合わせ窓口は使い回す（ドラッグ1セルごとに作らない）
        // Query windows are reused per player so a drag never allocates one per cell
        private readonly Dictionary<int, ConstructionWalletQuery> _queries = new();

        public ConstructionWalletService(IRemainingPlacementCountLookup lookup, IRemainingPlacementCountMutation mutation, ConstructionPayerDataStore payers)
        {
            _lookup = lookup;
            _mutation = mutation;
            _payers = payers;
        }

        // 問い合わせ後、確定でCommitPlacementを呼ぶ
        // Ask, then call CommitPlacement once final
        public IConstructionPlacementPlan PlanPlacement(BlockId blockId, int playerId)
        {
            // 窓口の答えを確定用Planへ詰め、判断は窓口内に閉じる
            // Pack the query answer into a commit plan, keeping decisions inside the query
            var query = GetQuery(playerId);
            if (!query.TryPlanCell(blockId, out var cell)) return new DirectCostPlacementPlan(query.GetItemsToConsume(blockId));
            return new WalletPlacementPlan(cell.ItemsToConsume, _mutation, _payers, cell.Usage, playerId, cell.WalletBlockId, cell.PlacementsPerCost);
        }

        public void CommitPlacement(IConstructionPlacementPlan plan, IOpenableInventory inventory, BlockInstanceId blockInstanceId)
        {
            plan.Commit(inventory, blockInstanceId);
        }

        // 問い合わせ後、確定でCommitRemovalを呼ぶ
        // Ask, then call CommitRemoval once final
        public IConstructionRemovalPlan PlanRemoval(BlockId blockId, BlockInstanceId blockInstanceId, int removePlayerId)
        {
            // 戻し先は撤去した人ではなく設置して支払った人の財布
            // The remainder goes back to whoever placed and paid for the block, not to whoever removes it
            var payerPlayerId = _payers.GetPayer(blockInstanceId, removePlayerId);
            var query = GetQuery(payerPlayerId);
            var refund = ConstructionCostService.CreateRefundItems(query.GetItemsToRefund(blockId));

            // 設置と同じ窓口で撤去を判断し、確定処理だけを予約する
            // Decide removal through the same query as placement and reserve only the commit
            if (!query.UsesWallet(blockId)) return new DirectCostRemovalPlan(refund);
            return new WalletRemovalPlan(refund, _mutation, _payers, payerPlayerId, ConstructionWalletQuery.ResolveWalletBlockId(blockId), blockInstanceId, query.WouldCondenseOnReturn(blockId));
        }

        public void CommitRemoval(IConstructionRemovalPlan plan)
        {
            plan.Commit();
        }

        private ConstructionWalletQuery GetQuery(int playerId)
        {
            if (_queries.TryGetValue(playerId, out var query)) return query;
            query = new ConstructionWalletQuery(_lookup.GetReader(playerId));
            _queries[playerId] = query;
            return query;
        }

        // 設置・撤去1操作の末尾で呼び、溜まった残り設置数の変更を財布ごと1通へ集約する
        // Called at the end of one place/remove operation to collapse the accumulated changes into one notification per wallet
        public void FlushRemainingCountChanges()
        {
            _mutation.FlushChanges();
        }
    }
}
