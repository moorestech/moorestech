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
            Network.DrainOccupancyChanges();
        }
        public void Apply(BeltTickDifference difference)
        {
            ValidateNextTick(difference.Tick);
            foreach (var change in difference.BeforeTick) change.Apply(Network);
            Advance(difference.Tick, difference.Outputs);
            foreach (var change in difference.AfterTick) change.Apply(Network);
            Network.DrainOccupancyChanges();
        }
        public void Advance(ulong tick, BeltOutputResult[] outputs)
        {
            // 初回snapshot以降の連続した確定tickだけを再現する。
            // Replay only consecutive committed ticks after the initial snapshot.
            ValidateNextTick(tick);
            receivers.SetResults(outputs);
            Network.Tick();
            Tick = tick;
        }
        private void ValidateNextTick(ulong tick)
        {
            if (tick != Tick + 1) throw new InvalidOperationException($"Expected belt tick {Tick + 1}, received {tick}.");
        }
    }
}
