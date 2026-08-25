using System;
using System.Collections.Generic;
using Core.Master;
using Game.Construction;
using UniRx;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     クライアント側の財布ミラー。サーバーの残り設置数を購読・初期データで持ち、読み取り口として差し出す
    ///     The client-side wallet mirror; it holds the server's remaining placements from subscription and initial data, and offers them as a read port
    /// </summary>
    public class ClientRemainingPlacementCountDatastore : IRemainingPlacementCountReader
    {
        public IObservable<Unit> OnWalletChanged => _onWalletChanged;
        private readonly Subject<Unit> _onWalletChanged = new();
        private readonly Dictionary<BlockId, int> _remainingCounts = new();

        // 生のBlockIdを受け、財布キーへの正規化は内側で行う
        // Takes a raw BlockId; normalizing it to the wallet key happens inside
        public int GetRemainingCount(BlockId blockId)
        {
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(blockId);
            return _remainingCounts.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
        }

        public void ApplyAll(IReadOnlyDictionary<BlockId, int> counts)
        {
            _remainingCounts.Clear();
            foreach (var (walletBlockId, remainingCount) in counts) _remainingCounts[walletBlockId] = remainingCount;
            _onWalletChanged.OnNext(Unit.Default);
        }

        internal void Apply(BlockId walletBlockId, int remainingCount)
        {
            _remainingCounts[walletBlockId] = remainingCount;
            _onWalletChanged.OnNext(Unit.Default);
        }
    }
}
