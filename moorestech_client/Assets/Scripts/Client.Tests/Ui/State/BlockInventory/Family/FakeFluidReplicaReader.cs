using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Fluid;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    // 手で流す通知と節を持つ偽の読み手
    // A fake reader with hand-driven notifications and nodes
    internal sealed class FakeFluidReplicaReader : IFluidReplicaReader
    {
        private readonly Subject<Unit> _changed = new();
        private readonly Dictionary<Vector3Int, (int fluidId, double amount, double capacity)> _nodes = new();

        public int SubscriberCount { get; private set; }

        public System.IObservable<Unit> OnReplicaChanged => Observable.Create<Unit>(observer =>
        {
            SubscriberCount++;
            var subscription = _changed.Subscribe(observer);
            return Disposable.Create(() =>
            {
                SubscriberCount--;
                subscription.Dispose();
            });
        });

        public void SetNode(Vector3Int position, int fluidId, double amount, double capacity) => _nodes[position] = (fluidId, amount, capacity);

        public void NotifyChanged() => _changed.OnNext(Unit.Default);

        public bool TryReadNode(Vector3Int position, out int fluidId, out double amount, out double capacity)
        {
            var found = _nodes.TryGetValue(position, out var node);
            (fluidId, amount, capacity) = node;
            return found;
        }
    }
}
