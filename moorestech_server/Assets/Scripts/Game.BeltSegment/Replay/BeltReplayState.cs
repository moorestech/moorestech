namespace Game.BeltSegment
{
    // 配列は確定済みの入力値。実行状態との分離はgraphが所有する。
    // Arrays carry finalized input values; the graph owns isolation from live state.
    public sealed class BeltReplaySnapshot
    {
        public readonly BeltReplaySegmentState[] Segments;
        public readonly BeltReplayLink[] Links;
        public readonly BeltReplayInput[] Inputs;
        public readonly BeltReplayOutput[] Outputs;

        public BeltReplaySnapshot(BeltReplaySegmentState[] segments, BeltReplayLink[] links,
            BeltReplayInput[] inputs, BeltReplayOutput[] outputs)
        {
            Segments = segments;
            Links = links;
            Inputs = inputs;
            Outputs = outputs;
        }
    }

    public readonly struct BeltReplaySegmentState
    {
        public readonly int Capacity, Speed, PriorityIndex;
        public readonly BeltSegmentKind Kind;
        public readonly BeltItemState[] Items;
        public readonly BeltItem? BufferedItem;

        private BeltReplaySegmentState(int capacity, int speed, BeltSegmentKind kind,
            int priorityIndex, BeltItemState[] items, BeltItem? bufferedItem)
        {
            Capacity = capacity;
            Speed = speed;
            Kind = kind;
            PriorityIndex = priorityIndex;
            Items = items;
            BufferedItem = bufferedItem;
        }

        // 種別ごとの不変条件を生成入口で固定する。
        // Fix kind-specific invariants at construction.
        public static BeltReplaySegmentState Normal(int capacity, int speed, BeltItemState[] items)
            => new BeltReplaySegmentState(capacity, speed, BeltSegmentKind.Normal, 0, items, null);
        public static BeltReplaySegmentState Merge(int speed, int priorityIndex, BeltItemState[] items, BeltItem? bufferedItem)
            => new BeltReplaySegmentState(1, speed, BeltSegmentKind.Merge, priorityIndex, items, bufferedItem);
        public static BeltReplaySegmentState Branch(int capacity, int speed, int priorityIndex, BeltItemState[] items, BeltItem? bufferedItem)
            => new BeltReplaySegmentState(capacity, speed, BeltSegmentKind.Branch, priorityIndex, items, bufferedItem);
    }

    public readonly struct BeltReplayLink
    {
        public readonly int SourceSegmentId, TargetSegmentId;
        public readonly BeltDirection OutputDirection;
        public BeltReplayLink(int sourceSegmentId, int targetSegmentId, BeltDirection outputDirection)
        {
            SourceSegmentId = sourceSegmentId;
            TargetSegmentId = targetSegmentId;
            OutputDirection = outputDirection;
        }
    }

    public readonly struct BeltReplayInput
    {
        public readonly int TargetSegmentId;
        public readonly BeltDirection InputDirection;
        public BeltReplayInput(int targetSegmentId, BeltDirection inputDirection)
        {
            TargetSegmentId = targetSegmentId;
            InputDirection = inputDirection;
        }
    }

    public readonly struct BeltReplayOutput
    {
        public readonly int SourceSegmentId;
        public readonly BeltDirection OutputDirection;
        public BeltReplayOutput(int sourceSegmentId, BeltDirection outputDirection)
        {
            SourceSegmentId = sourceSegmentId;
            OutputDirection = outputDirection;
        }
    }

    // 正の可否集合と順序付きの成功搬入で、完全な1tickを表す。
    // Positive availability sets and ordered insertions describe one complete tick.
    public sealed class BeltReplayTick
    {
        public readonly BeltReplaySpeedChange[] SpeedChanges;
        public readonly int[] ReadyInputs, SuccessfulOutputs;
        public readonly BeltReplayInsertion[] Insertions;
        public BeltReplayTick(BeltReplaySpeedChange[] speedChanges, int[] readyInputs,
            int[] successfulOutputs, BeltReplayInsertion[] insertions)
        {
            SpeedChanges = speedChanges;
            ReadyInputs = readyInputs;
            SuccessfulOutputs = successfulOutputs;
            Insertions = insertions;
        }
    }

    public readonly struct BeltReplaySpeedChange
    {
        public readonly int SegmentId, Speed;
        public BeltReplaySpeedChange(int segmentId, int speed)
        {
            SegmentId = segmentId;
            Speed = speed;
        }
    }

    public readonly struct BeltReplayInsertion
    {
        public readonly int InputId, Length;
        public readonly BeltItem Item;
        public BeltReplayInsertion(int inputId, int length, BeltItem item)
        {
            InputId = inputId;
            Length = length;
            Item = item;
        }
    }
}
