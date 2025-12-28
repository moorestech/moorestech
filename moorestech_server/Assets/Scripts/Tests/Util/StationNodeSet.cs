using System;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailGraph;

namespace Tests.Util
{
    // Shared station node bundle for test scenarios.
    public readonly struct StationNodeSet
    {
        public StationNodeSet(
            RailNode exitFront,
            RailNode entryFront,
            RailNode exitBack,
            RailNode entryBack,
            int segmentLength,
            int blockLength)
            : this(null, null, exitFront, entryFront, exitBack, entryBack, segmentLength, blockLength, false)
        {
        }

        public StationNodeSet(
            RailComponent entryComponent,
            RailComponent exitComponent,
            RailNode exitFront,
            RailNode entryFront,
            RailNode exitBack,
            RailNode entryBack,
            int segmentLength,
            int blockLength)
            : this(entryComponent, exitComponent, exitFront, entryFront, exitBack, entryBack, segmentLength, blockLength, true)
        {
        }

        private StationNodeSet(
            RailComponent entryComponent,
            RailComponent exitComponent,
            RailNode exitFront,
            RailNode entryFront,
            RailNode exitBack,
            RailNode entryBack,
            int segmentLength,
            int blockLength,
            bool includeComponents)
        {
            ExitFront = exitFront ?? throw new ArgumentNullException(nameof(exitFront));
            EntryFront = entryFront ?? throw new ArgumentNullException(nameof(entryFront));
            ExitBack = exitBack ?? throw new ArgumentNullException(nameof(exitBack));
            EntryBack = entryBack ?? throw new ArgumentNullException(nameof(entryBack));
            SegmentLength = segmentLength;
            BlockLength = blockLength > 0 ? blockLength : segmentLength;
            EntryComponent = includeComponents ? entryComponent : null;
            ExitComponent = includeComponents ? exitComponent : null;
        }

        public RailComponent EntryComponent { get; }
        public RailComponent ExitComponent { get; }
        public RailNode ExitFront { get; }
        public RailNode EntryFront { get; }
        public RailNode ExitBack { get; }
        public RailNode EntryBack { get; }
        public int SegmentLength { get; }
        public int BlockLength { get; }
    }
}
