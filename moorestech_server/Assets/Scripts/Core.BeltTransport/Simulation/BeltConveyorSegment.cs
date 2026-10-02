using System;

namespace Core.BeltTransport
{
    // 走行列・隙間・密着ブロックを管理するsegment。speedは0～MaxSpeedで、tick中は固定
    // gapsの先頭は出口までの距離、残りは直前アイテムの後端までの隙間
    // blockSizesは密着ブロックの先頭・末尾でだけ有効。totalGapはgapの合計を差分で維持する
    // 通常segmentは段階4、合流・分岐segmentはbufferにより段階1で更新する。外部供給と配線変更はtick境界で行う
    // A segment managing the item run, gaps and tightly-packed blocks. Speed is 0..MaxSpeed and fixed during a tick
    // The head gap is the distance to the exit; the others are distances to the previous item's rear
    // blockSizes is valid only at the first and last item of each block. totalGap keeps the sum of gaps incrementally
    // Normal segments update in stage 4, merge/branch segments in stage 1 via their buffer. Supply and wiring change at tick boundaries
    public sealed class BeltConveyorSegment : IBeltSource, IBeltReceiver
    {
        private readonly int[] _gaps;
        private readonly BeltItem[] _items;
        private readonly int[] _blockSizes;
        private int _head;
        private int _totalGap;
        private int _tickSpeed;
        private readonly IBeltSource[] _inputs = new IBeltSource[3];
        private BeltDirection _reservedInputDirection = BeltDirection.None;
        private BeltDirection _outputDirection;
        private BeltSegmentTransfer _deferredOutput;
        private int _inputCount;
        private int _inputOrder;
        private int _inputPorts;
        private int _inputMask;
        private int Length => Capacity * BeltConstants.ItemWidth;

        public BeltSegmentKind Kind { get; }
        public BeltBuffer Buffer { get; }
        public IBeltReceiver Output { get; private set; }
        public int Capacity { get; }
        public int Count { get; private set; }

        // blockへ保存する方向の優先順。合流は搬入方向、分岐はbufferの搬出方向、通常は0
        // Direction priority order saved to the block. Input order for a merge, buffer output order for a branch, 0 otherwise
        public int PriorityOrder => Kind == BeltSegmentKind.Merge ? _inputOrder
            : Kind == BeltSegmentKind.Branch ? Buffer.PriorityOrder : 0;
        internal int TickSpeed => _tickSpeed;
        public int Speed { get; private set; }

        // 先頭から最後尾アイテムの後端までの占有長。O(1)
        // Occupied length from the exit to the rear of the last item. O(1)
        private int TotalLength => _totalGap + BeltConstants.ItemWidth * Count;

        // このtickで先頭が出口を越える距離。正数なら搬出可能。空なら0
        // Distance the head passes the exit this tick. Positive means it can be output. 0 when empty
        private int OutputLength => Count == 0 ? 0 : _tickSpeed - _gaps[_head];

        // priorityOrderは未接続方向も含む3方向の順序。InitializeFromDirectionなら役割と向きから初期化する
        // forwardDirectionは合流の唯一の搬出方向、または分岐の終端マスの直進搬出方向
        // priorityOrder is the order of three directions including unconnected ones; InitializeFromDirection derives it from the role and direction
        // forwardDirection is the merge's only output direction, or the straight output direction of the branch's last cell
        public BeltConveyorSegment(int capacity, int speed, BeltSegmentKind kind, int priorityOrder, BeltDirection forwardDirection)
        {
            // 占有長がint内に収まる容量だけ許可する。合流は1マス固定
            // Allow only capacities whose occupied length fits in int. A merge is exactly one cell
            if (capacity <= 0 || capacity > (int.MaxValue - (BeltConstants.ItemWidth - 1)) / BeltConstants.ItemWidth ||
                (kind == BeltSegmentKind.Merge && capacity != 1))
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (speed < 0 || speed > BeltConstants.MaxSpeed) throw new ArgumentOutOfRangeException(nameof(speed));
            if (kind != BeltSegmentKind.Normal && kind != BeltSegmentKind.Merge && kind != BeltSegmentKind.Branch)
                throw new ArgumentOutOfRangeException(nameof(kind));
            Capacity = capacity;
            Speed = speed;
            Kind = kind;

            // 保存値が無ければ、合流は直進側の搬入、分岐は直進の搬出を先頭に初期化する
            // Without a saved value, a merge starts with the straight input and a branch with the straight output
            if (kind == BeltSegmentKind.Merge)
                _inputOrder = priorityOrder >= 0 ? priorityOrder
                    : BeltPriority.Create(BeltDirections.Opposite(forwardDirection));
            if (kind == BeltSegmentKind.Merge || kind == BeltSegmentKind.Branch)
                Buffer = new BeltBuffer(this, kind == BeltSegmentKind.Branch
                    ? (priorityOrder >= 0 ? priorityOrder : BeltPriority.Create(forwardDirection))
                    : (int)forwardDirection);
            _gaps = new int[capacity];
            _items = new BeltItem[capacity];
            _blockSizes = new int[capacity];
        }

        // 段階0より前に変更する。tick中は固定。0では走行しない
        // Change before stage 0. Fixed during a tick. 0 means no movement
        public void SetSpeed(int speed)
        {
            if (speed < 0 || speed > BeltConstants.MaxSpeed) throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        // 搬出先を登録し、相手の搬入元へ自分を追加する。相手へは自分の搬出方向の反対を渡す
        // Register the output target and add this segment to its inputs, passing the opposite of our output direction
        public void ConnectTo(IBeltReceiver target, BeltDirection outputDirection)
        {
            target.AttachInput(this, BeltDirections.Opposite(outputDirection));
            _outputDirection = outputDirection;
            Output = target;
        }

        // 搬入元と接続方向を登録する。方向ごとに搬入元の登録番号を2bitで持ち、優先順は登録順によらない
        // Register an input and its direction. Each direction keeps the input's slot in 2 bits; priority ignores registration order
        public void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
            _inputs[_inputCount] = source;
            _inputPorts |= _inputCount << ((int)inputDirection * 2);
            _inputMask |= 1 << (int)inputDirection;
            _inputCount++;
        }

        // 入口側の空き距離。最後尾が入口からはみ出している間は負
        // Free length at the entrance. Negative while the last item still sticks out of the entrance
        public int GetOffer(BeltDirection inputDirection)
        {
            // 合流は段階2で予約した方向からだけ受け入れる
            // A merge accepts only from the direction reserved in stage 2
            if (Kind == BeltSegmentKind.Merge && inputDirection != _reservedInputDirection)
                return 0;
            return Length - TotalLength;
        }

        // 空き以下の進入距離なら、進入距離を保って末尾へ追加する
        // When the entry length fits the offer, append at the tail keeping that entry length
        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            var offer = GetOffer(inputDirection);
            if (length > offer) return false;
            EnqueueTail(offer - length, item);

            // 合流は搬入に成功した方向を優先順の末尾へ移す
            // A merge moves the succeeded input direction to the end of its order
            if (Kind == BeltSegmentKind.Merge)
            {
                _inputOrder = BeltPriority.MoveLast(_inputOrder, (int)inputDirection);
            }
            return true;
        }

        public bool TryGetOutput(BeltDirection inputDirection)
        {
            return OutputLength > 0;
        }

        // 段階0。このtickの速度を固定する
        // Stage 0. Fix the speed used for this tick
        internal void BeginTick()
        {
            _tickSpeed = Speed;
        }

        // 更新対象の構築時だけ呼ぶ。通常segmentへの搬出だけを段階4の後で反映する接続にする。Outputは実際の接続先のまま
        // Call only when building the update lists. Only output into a normal segment is deferred; Output stays the real target
        internal BeltSegmentTransfer CacheTransfer()
        {
            var target = Output as BeltConveyorSegment;
            _deferredOutput = target != null && target.Kind == BeltSegmentKind.Normal
                ? new BeltSegmentTransfer(target, BeltDirections.Opposite(_outputDirection)) : null;
            return _deferredOutput;
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

        // 段階1。前進して出口でクランプし、bufferが空で先頭が出口ちょうどなら取り出す
        // Stage 1. Advance clamped at the exit; take the head when the buffer is empty and the head is exactly at the exit
        internal bool CollectForBuffer(out BeltItem item)
        {
            Advance(false);
            item = default;
            if (Buffer.HasItem || Count == 0 || _gaps[_head] != 0) return false;
            item = _items[_head];
            DequeueHead();
            return true;
        }

        // 段階4。段階3で受け取ったアイテムも含めて前進し、出口を越えた先頭を搬出先へ渡す
        // Stage 4. Advance including items received in stage 3, handing the head past the exit to the output
        internal void AdvanceAndTransfer()
        {
            var sent = false;
            var length = OutputLength;
            if (length > 0 && Output != null)
                sent = _deferredOutput != null
                    ? _deferredOutput.TryReceive(length, _items[_head])
                    : Output.TryReceive(BeltDirections.Opposite(_outputDirection), length, _items[_head]);
            Advance(sent);
        }

        // 段階4の全前進完了後、成立済みの通常segment間搬送を進入距離を保って末尾へ反映する
        // After all stage-4 advances, apply a settled normal-to-normal transfer at the tail keeping its entry length
        internal void ReceiveTransferred(int length, in BeltItem item)
        {
            EnqueueTail(Length - length - TotalLength, item);
        }

        // tick境界で、出口に近い順のアイテムと出口までの距離を複製する
        // At a tick boundary, copy items in exit order with their distance to the exit
        public BeltItemState[] CaptureItems()
        {
            var result = new BeltItemState[Count];
            var distance = 0;
            for (var i = 0; i < Count; i++)
            {
                var p = (_head + i) % Capacity;
                distance += _gaps[p] + (i == 0 ? 0 : BeltConstants.ItemWidth);
                result[i] = new BeltItemState(_items[p], distance);
            }
            return result;
        }

        // 再生成した空のsegmentへ出口に近い順のアイテムを復元する。距離・間隔・容量は呼び出し側が確定させる
        // Restore items in exit order into a rebuilt empty segment. The caller settles distances, spacing and capacity
        public void RestoreItems(BeltItemState[] restoredItems)
        {
            foreach (var state in restoredItems)
                EnqueueTail(state.DistanceToExit - TotalLength, state.Item);
        }

        // 末尾へ追加する。gapは現在の最後尾の後端（空なら出口）からの距離
        // Append at the tail. gap is measured from the current last item's rear (or the exit when empty)
        private void EnqueueTail(int gap, in BeltItem item)
        {
            var p = _head + Count;
            if (p >= Capacity) p -= Capacity;
            _items[p] = item;
            SetPhysicalGap(p, gap);

            // 密着していれば末尾ブロックを1つ伸ばし、両端の個数を書き戻す
            // When packed, extend the tail block by one and write the size back to both ends
            if (Count == 0 || gap != 0)
            {
                _blockSizes[p] = 1;
            }
            else
            {
                var tail = p == 0 ? Capacity - 1 : p - 1;
                var size = _blockSizes[tail] + 1;
                var start = p - size + 1;
                if (start < 0) start += Capacity;
                _blockSizes[start] = _blockSizes[p] = size;
            }
            Count++;
        }

        // 1tick前進する。sentは先頭の搬出が成立したか
        // Advance one tick. sent tells whether the head item's output succeeded
        private void Advance(bool sent)
        {
            if (Count == 0) return;

            // 搬出成立: 先頭を除き、残りは速度分そのまま進む
            // Output succeeded: drop the head and move the rest rigidly by the speed
            var gapToExit = _gaps[_head];
            if (sent)
            {
                DequeueHead();
                if (Count > 0) SetPhysicalGap(_head, _gaps[_head] - _tickSpeed);
                return;
            }

            // 先頭が出口に届かなければ全体がそのまま進む
            // When the head does not reach the exit, the whole run moves rigidly
            if (gapToExit >= _tickSpeed)
            {
                SetPhysicalGap(_head, gapToExit - _tickSpeed);
                return;
            }

            // 受け入れ拒否: 先頭を出口に置き、残り進行量でブロック境界の隙間を詰める
            // Rejected: park the head at the exit and close block-boundary gaps with the remaining advance
            var remaining = _tickSpeed - gapToExit;
            if (_gaps[_head] != 0) SetPhysicalGap(_head, 0);
            while (_blockSizes[_head] < Count && remaining > 0)
            {
                var phys = _head + _blockSizes[_head];
                if (phys >= Capacity) phys -= Capacity;
                var gap = _gaps[phys];
                if (gap <= remaining)
                {
                    // 後続ブロックが接触し、先頭ブロックへ合体する
                    // The following block touches and merges into the head block
                    SetPhysicalGap(phys, 0);
                    remaining -= gap;
                    var size = _blockSizes[phys];
                    var tail = phys + size - 1;
                    if (tail >= Capacity) tail -= Capacity;
                    _blockSizes[_head] += size;
                    _blockSizes[tail] = _blockSizes[_head];
                }
                else
                {
                    SetPhysicalGap(phys, gap - remaining);
                    break;
                }
            }
        }

        // 搬出またはbufferへの保存が確定した先頭を取り除く。残ったアイテムの位置は変えない。O(1)
        // Remove the head whose output or buffering is settled. Remaining items keep their positions. O(1)
        private void DequeueHead()
        {
            var gapToExit = _gaps[_head];
            var size = _blockSizes[_head] - 1;
            _items[_head] = default;
            SetPhysicalGap(_head, 0);
            _head++;
            if (_head == Capacity) _head = 0;
            Count--;

            // 次の先頭のgapを出口までの距離に補正する
            // Convert the next head's gap into its distance to the exit
            if (Count > 0) SetPhysicalGap(_head, _gaps[_head] + gapToExit + BeltConstants.ItemWidth);

            // 先頭ブロックが残るなら両端へ個数を書き戻す
            // When the head block remains, write its size back to both ends
            if (size > 0)
            {
                var tail = _head + size - 1;
                if (tail >= Capacity) tail -= Capacity;
                _blockSizes[_head] = _blockSizes[tail] = size;
            }
        }

        private void SetPhysicalGap(int phys, int value)
        {
            // 挿入位置とLengthの上限により、占有長はint内に収まる
            // Insert positions and the Length bound keep the occupied length within int
            _totalGap = _totalGap - _gaps[phys] + value;
            _gaps[phys] = value;
        }
    }
}
