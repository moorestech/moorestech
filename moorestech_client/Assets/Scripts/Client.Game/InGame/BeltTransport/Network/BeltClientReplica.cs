using System;
using System.Collections.Generic;
using Core.BeltTransport;
using UniRx;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltClientReplica : IBeltItemDropObserver
    {
        private readonly BeltNetworkReplay _replay;
        private readonly SortedDictionary<ulong, BeltTickDifference> _pending = new();
        private readonly Subject<BeltNetworkSnapshot> _changed = new();
        public IObservable<BeltNetworkSnapshot> OnStateChanged => _changed;
        private ulong Tick => _replay.Tick;
        public BeltNetworkSnapshot Snapshot => _replay.Network.Capture();
        public BeltClientReplica(BeltCommittedSnapshot initial) { _replay = new(initial.Tick, initial.Snapshot, this); }
        public void Receive(BeltTickDifference difference)
        {
            // 初期境界以前と重複通知は再実行しない。
            // Do not replay notifications at/before the initial boundary or duplicates.
            if (difference.Tick <= Tick || _pending.ContainsKey(difference.Tick))
            {
                Debug.Log($"Belt tick {difference.Tick} already represented at boundary {Tick}; ignored.");
                return;
            }
            _pending.Add(difference.Tick, difference);
            if (Tick + 1 < difference.Tick) Debug.LogWarning($"Belt transport waiting for tick {Tick + 1}; received {difference.Tick}.");
            bool advanced = false;
            while (_pending.TryGetValue(Tick + 1, out var next))
            {
                _pending.Remove(Tick + 1);
                _replay.Apply(next);
                advanced = true;
            }
            if (advanced) _changed.OnNext(Snapshot);
        }
        public void OnDropped(BeltCellItemState item, string reason) => Debug.LogWarning($"Belt item {item.Item.Guid} dropped at cell {item.CellId}: {reason}");
    }
}
