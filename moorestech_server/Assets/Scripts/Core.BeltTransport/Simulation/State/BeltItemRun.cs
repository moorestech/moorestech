namespace Core.BeltTransport
{
    // gapsの先頭は出口までの距離、残りは直前アイテムの後端までの隙間
    // blockSizesは密着ブロックの先頭・末尾でだけ有効。totalGapはgapの合計を差分で維持する
    // The head gap is the distance to the exit; the others are distances to the previous item's rear
    // blockSizes is valid only at the first and last item of each block. totalGap keeps the sum of gaps incrementally
    internal sealed class BeltItemRun
    {
        private readonly int[] _gaps;
        private readonly BeltItem[] _items;
        private readonly int[] _blockSizes;
        private int _head;
        private int _totalGap;
        private int Length => Capacity * BeltConstants.ItemWidth;
        private int Capacity { get; }
        internal int Count { get; private set; }
        // 先頭から最後尾アイテムの後端までの占有長。O(1)
        // Occupied length from the exit to the rear of the last item. O(1)
        internal int TotalLength => _totalGap + BeltConstants.ItemWidth * Count;
        internal BeltItem HeadItem => _items[_head];

        internal BeltItemRun(int capacity)
        {
            Capacity = capacity;
            _gaps = new int[capacity];
            _items = new BeltItem[capacity];
            _blockSizes = new int[capacity];
        }

        internal int GetOutputLength(int tickSpeed) => Count == 0 ? 0 : tickSpeed - _gaps[_head];

        // 既存の取り出しで隙間・密着列も一緒に消費する
        // Consume gaps and packed groups through the existing dequeue operation
        internal void ClearItems()
        {
            while (Count > 0) DequeueHead();
        }

        // tick境界で、出口に近い順のアイテムと出口までの距離を複製する
        // At a tick boundary, copy items in exit order with their distance to the exit
        internal BeltItemState[] CaptureItems()
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
        internal void RestoreItems(BeltItemState[] restoredItems)
        {
            foreach (var state in restoredItems)
                EnqueueTail(state.DistanceToExit - TotalLength, state.Item);
        }

        // 進入距離lengthで入口から入ったアイテムを、現在の最後尾の後ろへ追加する
        // Append an item that entered the entrance by length behind the current last item
        internal void EnqueueAtEntry(int length, in BeltItem item)
        {
            EnqueueTail(Length - length - TotalLength, item);
        }

        // 先頭が出口ちょうどにあれば取り出す。残ったアイテムの位置は変えない
        // Take the head when it is exactly at the exit. Remaining items keep their positions
        internal bool TryDequeueHeadAtExit(out BeltItem item)
        {
            item = default;
            if (Count == 0 || _gaps[_head] != 0) return false;
            item = _items[_head];
            DequeueHead();
            return true;
        }

        // 末尾へ追加する。gapは現在の最後尾の後端（空なら出口）からの距離
        // Append at the tail. gap is measured from the current last item's rear (or the exit when empty)
        internal void EnqueueTail(int gap, in BeltItem item)
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
        internal void Advance(bool sent, int tickSpeed)
        {
            if (Count == 0) return;

            // 搬出成立: 先頭を除き、残りは速度分そのまま進む
            // Output succeeded: drop the head and move the rest rigidly by the speed
            var gapToExit = _gaps[_head];
            if (sent)
            {
                DequeueHead();
                if (Count > 0) SetPhysicalGap(_head, _gaps[_head] - tickSpeed);
                return;
            }

            // 先頭が出口に届かなければ全体がそのまま進む
            // When the head does not reach the exit, the whole run moves rigidly
            if (gapToExit >= tickSpeed)
            {
                SetPhysicalGap(_head, gapToExit - tickSpeed);
                return;
            }

            // 受け入れ拒否: 先頭を出口に置き、残り進行量でブロック境界の隙間を詰める
            // Rejected: park the head at the exit and close block-boundary gaps with the remaining advance
            var remaining = tickSpeed - gapToExit;
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
