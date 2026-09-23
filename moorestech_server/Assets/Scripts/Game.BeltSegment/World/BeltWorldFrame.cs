namespace Game.BeltSegment
{
    public sealed class BeltWorldFrame
    {
        public readonly BeltStreamPosition Previous, Position;
        public readonly ulong Generation;
        public readonly uint PreviousHash;
        public readonly BeltReplayTick Replay;
        public BeltWorldFrame(BeltStreamPosition previous, BeltStreamPosition position, ulong generation, uint previousHash, BeltReplayTick replay)
        { Previous = previous; Position = position; Generation = generation; PreviousHash = previousHash; Replay = replay; }
    }
}
