using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using UniRx;
using static Server.Event.EventReceive.RemainingPlacementCountChangedEventPacket;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     クライアント側の財布。残り設置数を購読/初期データで持ち、置ける数の判断もここに閉じる
    ///     The client-side wallet; holds remaining placements from subscription/initial data and owns every judgement about how many cells are placeable
    /// </summary>
    public class ClientRemainingPlacementCountDatastore
    {
        public IObservable<Unit> OnRemainingPlacementCountChanged => _onRemainingPlacementCountChanged;
        private readonly Subject<Unit> _onRemainingPlacementCountChanged = new();
        private readonly Dictionary<BlockId, int> _remainingCounts = new();

        // 生のBlockIdを受け、財布キーへの正規化は内側で行う
        // Takes a raw BlockId; normalizing it to the wallet key happens inside
        public int GetRemainingCount(BlockId blockId)
        {
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(blockId);
            return _remainingCounts.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
        }

        // 財布の残りと所持素材で何セル置けるか
        // How many cells the wallet remainder plus the held materials can cover
        public int GetAffordablePlacementCount(BlockId blockId, IEnumerable<IItemStack> inventoryItems)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var affordableSets = ConstructionMaterialAffordability.CalculateAffordableCellCount(blockMaster.RequiredItems, inventoryItems);

            // 設置数/1セット=1は財布を素通りし、セル数がそのまま置ける数になる
            // placementsPerCost==1 bypasses the wallet, so the set count is the placement count
            if (blockMaster.PlacementsPerCost <= 1 || affordableSets == int.MaxValue) return affordableSets;

            // 大量所持でのオーバーフローを避ける
            // Avoid overflow on very large holdings
            var total = GetRemainingCount(blockId) + (long)affordableSets * blockMaster.PlacementsPerCost;
            return int.MaxValue < total ? int.MaxValue : (int)total;
        }

        public void ApplyAll(IReadOnlyDictionary<BlockId, int> counts)
        {
            _remainingCounts.Clear();
            foreach (var (walletBlockId, remainingCount) in counts) _remainingCounts[walletBlockId] = remainingCount;
            _onRemainingPlacementCountChanged.OnNext(Unit.Default);
        }

        internal void Apply(BlockId walletBlockId, int remainingCount)
        {
            _remainingCounts[walletBlockId] = remainingCount;
            _onRemainingPlacementCountChanged.OnNext(Unit.Default);
        }
    }
}
