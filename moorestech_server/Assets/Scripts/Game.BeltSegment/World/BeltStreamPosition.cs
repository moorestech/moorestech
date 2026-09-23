namespace Game.BeltSegment
{
    public readonly struct BeltStreamPosition
    {
        public readonly ulong Tick;
        public readonly uint Sequence;
        public BeltStreamPosition(ulong tick, uint sequence) { Tick = tick; Sequence = sequence; }
    }
}
