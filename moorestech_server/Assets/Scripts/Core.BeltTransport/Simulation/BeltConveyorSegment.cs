using System;

namespace Core.BeltTransport
{
    // 走行列・隙間・密着ブロックを管理するsegment。speedは0～MaxSpeedで、tick中は固定
    // gapsの先頭は出口までの距離、残りは直前アイテムの後端までの隙間
    // blockSizesは密着ブロックの先頭・末尾でだけ有効。totalGapはgapの合計を差分で維持する
    // A segment managing the item run, gaps and tightly-packed blocks. Speed is 0..MaxSpeed and fixed during a tick
    // The head gap is the distance to the exit; the others are distances to the previous item's rear
    // blockSizes is valid only at the first and last item of each block. totalGap keeps the sum of gaps incrementally
    public sealed class BeltConveyorSegment
    {
        private readonly int[] _gaps;
        private readonly BeltItem[] _items;
        private readonly int[] _blockSizes;
        private int _head;
        private int _totalGap;
        private int _tickSpeed;
        private int Length => Capacity * BeltConstants.ItemWidth;

        public int Capacity { get; }
        public int Count { get; private set; }
        public int Speed { get; private set; }

        // 先頭から最後尾アイテムの後端までの占有長。O(1)
        // Occupied length from the exit to the rear of the last item. O(1)
        private int TotalLength => _totalGap + BeltConstants.ItemWidth * Count;

        // このtickで先頭が出口を越える距離。正数なら搬出可能。空なら0
        // Distance the head passes the exit this tick. Positive means it can be output. 0 when empty
        private int OutputLength => Count == 0 ? 0 : _tickSpeed - _gaps[_head];

        public BeltConveyorSegment(int capacity, int speed)
        {
            // 占有長がint内に収まる容量だけ許可する
            // Allow only capacities whose occupied length fits in int
            if (capacity <= 0 || capacity > (int.MaxValue - (BeltConstants.ItemWidth - 1)) / BeltConstants.ItemWidth)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (speed < 0 || speed > BeltConstants.MaxSpeed) throw new ArgumentOutOfRangeException(nameof(speed));
            Capacity = capacity;
            Speed = speed;
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

        // 段階0。このtickの速度を固定する
        // Stage 0. Fix the speed used for this tick
        internal void BeginTick()
        {
            _tickSpeed = Speed;
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
