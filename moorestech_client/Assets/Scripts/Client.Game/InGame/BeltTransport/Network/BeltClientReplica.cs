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
        public void Receive(BeltTickDifference difference)
        {
            // 初回snapshotに含まれた通知を既存の受信バッファから除く。
            // Exclude buffered notifications already represented by the initial snapshot.
            if (difference.Tick <= _initialTick)
            {
                Debug.Log($"Belt tick {difference.Tick} already represented by initial boundary {_initialTick}; ignored.");
                return;
            }
            _replay.Apply(difference);
            _changed.OnNext(Snapshot);
        }
        public void OnDropped(BeltCellItemState item, string reason) => Debug.LogWarning($"Belt item {item.Item.Guid} dropped at cell {item.CellId}: {reason}");
    }
}
