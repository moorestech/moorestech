using System;
using System.Collections.Generic;
using Core.Master;
using UniRx;
using static Server.Event.EventReceive.RemainingPlacementCountChangedEventPacket;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     残り設置数の参照モデル。購読/初期データのみ更新
    ///     Remaining-placement model; updated only via subscription/initial data
    /// </summary>
    public class ClientRemainingPlacementCountDatastore
    {
        public IObservable<Unit> OnRemainingPlacementCountChanged => _onRemainingPlacementCountChanged;
        private readonly Subject<Unit> _onRemainingPlacementCountChanged = new();
        private readonly Dictionary<BlockId, int> _remainingCounts = new();

        public int GetRemainingCount(BlockId walletBlockId)
        {
            return _remainingCounts.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
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
