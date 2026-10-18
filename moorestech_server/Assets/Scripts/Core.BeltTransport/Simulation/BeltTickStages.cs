namespace Core.BeltTransport
{
    // 段階0: 全segmentの速度を固定する
    // Stage 0: fix every segment's speed for the tick
    internal sealed class BeltBeginTickStage : IBeltTickStage
    {
        private readonly BeltConveyorSegment[] _segments;
        internal BeltBeginTickStage(BeltConveyorSegment[] segments) { _segments = segments; }
        public int Count => _segments.Length;
        public void Run(int index) { _segments[index].BeginTick(); }
    }

    // 段階1: buffer付きsegmentを前進させ、先頭を回収する
    // Stage 1: advance buffered segments and collect their heads
    internal sealed class BeltBufferCollectStage : IBeltTickStage
    {
        private readonly BeltBuffer[] _buffers;
        internal BeltBufferCollectStage(BeltBuffer[] buffers) { _buffers = buffers; }
        public int Count => _buffers.Length;
        public void Run(int index) { _buffers[index].Collect(); }
    }

    // 段階2: 合流が搬入元を予約する
    // Stage 2: merges reserve their input
    internal sealed class BeltMergeResolveStage : IBeltTickStage
    {
        private readonly BeltMergeSegment[] _merges;
        internal BeltMergeResolveStage(BeltMergeSegment[] merges) { _merges = merges; }
        public int Count => _merges.Length;
        public void Run(int index) { _merges[index].ResolveInput(); }
    }

    // 段階3: bufferが搬出する
    // Stage 3: buffers transfer out
    internal sealed class BeltBufferTransferStage : IBeltTickStage
    {
        private readonly BeltBuffer[] _buffers;
        internal BeltBufferTransferStage(BeltBuffer[] buffers) { _buffers = buffers; }
        public int Count => _buffers.Length;
        public void Run(int index) { _buffers[index].Transfer(); }
    }

    // 段階4a: 通常→通常の空きを記録する
    // Stage 4a: capture every normal-to-normal offer
    internal sealed class BeltOfferCaptureStage : IBeltTickStage
    {
        private readonly BeltSegmentTransfer[] _transfers;
        internal BeltOfferCaptureStage(BeltSegmentTransfer[] transfers) { _transfers = transfers; }
        public int Count => _transfers.Length;
        public void Run(int index) { _transfers[index].CaptureOffer(); }
    }

    // 段階4b: 通常segmentを前進・搬出させる
    // Stage 4b: advance normal segments and output
    internal sealed class BeltNormalAdvanceStage : IBeltTickStage
    {
        private readonly BeltNormalSegment[] _normal;
        internal BeltNormalAdvanceStage(BeltNormalSegment[] normal) { _normal = normal; }
        public int Count => _normal.Length;
        public void Run(int index) { _normal[index].AdvanceAndTransfer(); }
    }

    // 段階4c: 成立した通常→通常の搬送を反映する
    // Stage 4c: apply the settled normal-to-normal transfers
    internal sealed class BeltTransferApplyStage : IBeltTickStage
    {
        private readonly BeltSegmentTransfer[] _transfers;
        internal BeltTransferApplyStage(BeltSegmentTransfer[] transfers) { _transfers = transfers; }
        public int Count => _transfers.Length;
        public void Run(int index) { _transfers[index].Apply(); }
    }
}
