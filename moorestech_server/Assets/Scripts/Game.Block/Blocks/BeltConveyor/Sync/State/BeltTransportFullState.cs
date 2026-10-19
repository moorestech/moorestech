namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // ワールド全体のベルト搬送のtick境界での全量。添字がsegment番号で、差分はこの番号で指す
    // The full state of the world-wide belt transport at a tick boundary; the index is the segment number that diffs refer to
    public sealed class BeltTransportFullState
    {
        public readonly BeltSegmentState[] Segments;

        public BeltTransportFullState(BeltSegmentState[] segments)
        {
            Segments = segments;
        }
    }
}
