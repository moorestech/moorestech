namespace Core.BeltTransport
{
    // 搬入元が最大1つで、段階4で前進して出口を越えた先頭を搬出先へ渡す通常segment
    // A normal segment with at most one input; it advances in stage 4 and hands the head past the exit to its output
    public sealed class BeltNormalSegment : BeltConveyorSegment, IBeltSource
    {
        private BeltDirection _outputDirection;
        private BeltEntryDirection _outputEntryDirection;
        private BeltSegmentTransfer _deferredOutput;

        public override BeltSegmentKind Kind => BeltSegmentKind.Normal;
        public IBeltReceiver Output { get; private set; }

        public BeltNormalSegment(int capacity, int speed) : base(capacity, speed)
        {
        }

        // 搬出先を登録し、相手の搬入元へ自分を追加する。相手へは自分の搬出方向の反対を渡す
        // Register the output target and add this segment to its inputs, passing the opposite of our output direction
        // entryDirectionは受け入れ側マスから見た12通りの進入方向で、渡すアイテムへ書く
        // entryDirection is the 12-way entry direction seen from the receiving cell, written into handed-over items
        public void ConnectTo(IBeltReceiver target, BeltDirection outputDirection, BeltEntryDirection entryDirection)
        {
            target.AttachInput(this, BeltDirections.Opposite(outputDirection));
            _outputDirection = outputDirection;
            _outputEntryDirection = entryDirection;
            Output = target;
        }

        public bool TryGetOutput(BeltDirection inputDirection)
        {
            return OutputLength > 0;
        }

        // 更新対象の構築時だけ呼ぶ。通常segmentへの搬出だけを段階4の後で反映する接続にする。Outputは実際の接続先のまま
        // Call only when building the update lists. Only output into a normal segment is deferred; Output stays the real target
        internal BeltSegmentTransfer CacheTransfer()
        {
            var target = Output as BeltNormalSegment;
            _deferredOutput = target != null
                ? new BeltSegmentTransfer(target, BeltDirections.Opposite(_outputDirection)) : null;
            return _deferredOutput;
        }

        // 段階4。段階3で受け取ったアイテムも含めて前進し、出口を越えた先頭を搬出先へ渡す
        // Stage 4. Advance including items received in stage 3, handing the head past the exit to the output
        internal void AdvanceAndTransfer()
        {
            var sent = false;
            var length = OutputLength;
            if (length > 0 && Output != null)
            {
                // 搬出先のマスへ入るので、渡す複製へ接続の進入方向を書く
                // The item enters the target's cell, so the handed copy carries the connection's entry direction
                var item = HeadItem.WithEntryDirection(_outputEntryDirection);
                sent = _deferredOutput != null
                    ? _deferredOutput.TryReceive(length, item)
                    : Output.TryReceive(BeltDirections.Opposite(_outputDirection), length, item);
            }
            Advance(sent);
        }

        // 段階4の全前進完了後、成立済みの通常segment間搬送を進入距離を保って末尾へ反映する
        // After all stage-4 advances, apply a settled normal-to-normal transfer at the tail keeping its entry length
        internal void ReceiveTransferred(int length, in BeltItem item)
        {
            EnqueueAtEntry(length, item);
        }
    }
}
