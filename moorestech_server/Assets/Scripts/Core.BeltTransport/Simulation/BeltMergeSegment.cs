namespace Core.BeltTransport
{
    // 1マス固定で最大3方向から搬入する合流segment。段階2で予約した方向からだけ受け入れる
    // A one-cell merge segment with up to three inputs. It accepts only from the direction reserved in stage 2
    public sealed class BeltMergeSegment : BeltBufferedSegment
    {
        private readonly IBeltSource[] _inputs = new IBeltSource[3];
        private BeltDirection _reservedInputDirection = BeltDirection.None;
        private int _inputCount;
        private int _inputOrder;
        private int _inputPorts;
        private int _inputMask;

        public override BeltSegmentKind Kind => BeltSegmentKind.Merge;
        public override int PriorityOrder => _inputOrder;

        // priorityOrderは未接続方向も含む3方向の搬入順。InitializeFromDirectionなら直進側の搬入を先頭に初期化する
        // forwardDirectionは唯一の搬出方向
        // priorityOrder is the input order of three directions including unconnected ones; InitializeFromDirection starts with the straight input
        // forwardDirection is the only output direction
        public BeltMergeSegment(int speed, int priorityOrder, BeltDirection forwardDirection) : base(1, speed, (int)forwardDirection)
        {
            _inputOrder = priorityOrder >= 0 ? priorityOrder
                : BeltPriority.Create(BeltDirections.Opposite(forwardDirection));
        }

        // 搬入元と接続方向を登録する。方向ごとに搬入元の登録番号を2bitで持ち、優先順は登録順によらない
        // Register an input and its direction. Each direction keeps the input's slot in 2 bits; priority ignores registration order
        public override void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
            _inputs[_inputCount] = source;
            _inputPorts |= _inputCount << ((int)inputDirection * 2);
            _inputMask |= 1 << (int)inputDirection;
            _inputCount++;
        }

        // 段階2で予約した方向からだけ受け入れる
        // Accept only from the direction reserved in stage 2
        public override int GetOffer(BeltDirection inputDirection)
        {
            if (inputDirection != _reservedInputDirection) return 0;
            return base.GetOffer(inputDirection);
        }

        // 搬入に成功した方向を優先順の末尾へ移す
        // Move the succeeded input direction to the end of the order
        public override bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            if (!base.TryReceive(inputDirection, length, item)) return false;
            _inputOrder = BeltPriority.MoveLast(_inputOrder, (int)inputDirection);
            return true;
        }

        // 段階2。空の合流segmentについて、搬入優先順に問い合わせて搬入元1つまたは搬入なしを予約する
        // Stage 2. For an empty merge, query inputs in priority order and reserve one input or none
        internal void ResolveInput()
        {
            _reservedInputDirection = BeltDirection.None;
            if (Count != 0) return;
            for (var offset = 0; offset < 3; offset++)
            {
                var direction = (BeltDirection)BeltPriority.Direction(_inputOrder, offset);
                if ((_inputMask & (1 << (int)direction)) == 0) continue;
                var port = (_inputPorts >> ((int)direction * 2)) & 3;
                if (!_inputs[port].TryGetOutput(direction)) continue;
                _reservedInputDirection = direction;
                return;
            }
        }
    }
}
