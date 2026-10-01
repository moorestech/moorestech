using System;
namespace Core.BeltTransport
{
    public sealed class BeltNetworkReplay
    {
        private readonly BeltReplayReceiverFactory receivers;
        public BeltTransportNetwork Network { get; }
        public ulong Tick { get; private set; }
        public BeltNetworkReplay(ulong tick, BeltNetworkSnapshot snapshot, IBeltItemDropObserver dropObserver)
        {
            Tick = tick;
            receivers = new BeltReplayReceiverFactory();
            Network = new BeltTransportNetwork(receivers, dropObserver);
            Network.Restore(snapshot);
        }
        public void Apply(BeltTickDifference difference)
        {
            // 初回snapshot以降の連続した確定tickだけを再現する。
            // Replay only consecutive committed ticks after the initial snapshot.
            if (difference.Tick != Tick + 1) throw new InvalidOperationException($"Expected belt tick {Tick + 1}, received {difference.Tick}.");
            foreach (var change in difference.BeforeTick) change.Apply(Network);
            receivers.SetResults(difference.Outputs);
            Network.Tick();
            foreach (var change in difference.AfterTick) change.Apply(Network);
            Tick = difference.Tick;
        }
    }
}
