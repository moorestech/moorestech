using System;
using System.Collections.Generic;
using Core.Master;
using UniRx;
using static Server.Event.EventReceive.RemainingPlacementCountChangedEventPacket;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     残り設置数の参照モデル(非MonoBehaviour)。購読・初期データからのみ更新する（前例 ClientHotbarDatastore）
    ///     Client-side model of remaining placements; updated only from the subscription/initial data (precedent: ClientHotbarDatastore)
    /// </summary>
    public class ClientRemainingPlacementCountDatastore
    {
        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();
        private readonly Dictionary<BlockId, int> _remainingCounts = new();

        public int GetRemainingCount(BlockId walletBlockId)
        {
            return _remainingCounts.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
        }

        public void ApplyAll(RemainingPlacementCountMessagePack[] counts)
        {
            _remainingCounts.Clear();
            foreach (var count in counts) _remainingCounts[new BlockId(count.WalletBlockId)] = count.RemainingCount;
            _onChanged.OnNext(Unit.Default);
        }

        public void Apply(int walletBlockId, int remainingCount)
        {
            _remainingCounts[new BlockId(walletBlockId)] = remainingCount;
            _onChanged.OnNext(Unit.Default);
        }
    }
}
