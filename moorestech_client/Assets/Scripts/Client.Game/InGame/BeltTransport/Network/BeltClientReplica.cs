using System;
using Core.BeltTransport;
using UniRx;
using UnityEngine;
namespace Client.Game.InGame.BeltTransport
{
    public sealed class BeltClientReplica : IBeltItemDropObserver
    {
        private readonly BeltNetworkReplay _replay;
        private readonly Subject<BeltNetworkSnapshot> _changed = new();
        public IObservable<BeltNetworkSnapshot> OnStateChanged => _changed;
        public BeltNetworkSnapshot Snapshot => _replay.Network.Capture();
        public BeltClientReplica(BeltCommittedSnapshot initial)
        { _replay = new(initial.Tick, initial.Snapshot, this); }
        public void Receive(BeltTickDifference difference)
        {
            _replay.Apply(difference);
            _changed.OnNext(Snapshot);
        }
        public void OnDropped(BeltCellItemState item, string reason) => Debug.LogWarning($"Belt item {item.Item.Guid} dropped at cell {item.CellId}: {reason}");
    }
}
