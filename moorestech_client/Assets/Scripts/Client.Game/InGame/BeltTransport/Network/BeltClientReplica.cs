using System;
using Core.BeltTransport;
using UniRx;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltClientReplica : IBeltItemDropObserver
    {
        private readonly BeltNetworkReplay _replay;
        private readonly ulong _initialTick;
        private readonly Subject<BeltNetworkSnapshot> _changed = new();
        public IObservable<BeltNetworkSnapshot> OnStateChanged => _changed;
        public BeltNetworkSnapshot Snapshot => _replay.Network.Capture();
        public BeltClientReplica(BeltCommittedSnapshot initial)
        { _initialTick = initial.Tick; _replay = new(initial.Tick, initial.Snapshot, this); }
        internal void ApplyChange(ulong tick, BeltBoundaryChange change)
        {
            if (tick <= _initialTick) return;
            change.Apply(_replay.Network);
        }
        internal void Advance(ulong tick, BeltOutputResult[] outputs)
        {
            if (tick <= _initialTick) return;
            _replay.Advance(tick, outputs);
        }
        internal void CompleteTick(ulong tick)
        {
            if (tick <= _initialTick) return;
            // 全seqの適用後に確定状態を描画へ渡す。
            // Publish the committed render state after every sequence has applied.
            _replay.Network.DrainOccupancyChanges();
            _changed.OnNext(Snapshot);
        }
        public void OnDropped(BeltCellItemState item, string reason) => Debug.LogWarning($"Belt item {item.Item.Guid} dropped at cell {item.CellId}: {reason}");
    }
}
