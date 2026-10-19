namespace Game.Block.Blocks.BeltConveyor.Sync.Diff
{
    // そのtickからsegmentが進む速度。歯車の回転数が変わったtickだけ載る
    // The speed a segment advances at from this tick on; present only on ticks where the gear rotation changed
    public readonly struct BeltSegmentSpeedRecord
    {
        public readonly int SegmentIndex;
        public readonly int Speed;

        public BeltSegmentSpeedRecord(int segmentIndex, int speed)
        {
            SegmentIndex = segmentIndex;
            Speed = speed;
        }
    }
}
