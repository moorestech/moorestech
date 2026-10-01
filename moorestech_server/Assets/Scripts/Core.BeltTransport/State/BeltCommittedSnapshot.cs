namespace Core.BeltTransport
{
    public sealed class BeltCommittedSnapshot
    {
        public readonly ulong Tick;
        public readonly BeltNetworkSnapshot Snapshot;
        public BeltCommittedSnapshot(ulong tick, BeltNetworkSnapshot snapshot) { Tick = tick; Snapshot = snapshot; }
    }
}
