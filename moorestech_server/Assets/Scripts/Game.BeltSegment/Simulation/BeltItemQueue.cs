namespace Game.BeltSegment
{
    /// <summary>走行列のring、隙間、密着ブロックを保持する。</summary>
    internal sealed class BeltItemQueue
    {
        readonly int n;
        readonly int[] gaps;
        readonly BeltItem[] items;
        readonly int[] blockSizes;
        int head, count;
        int totalGap;

        internal int Count => count;
        internal int TotalLength => totalGap + BeltConstants.ItemWidth * count;
        internal int HeadGap => count == 0 ? 0 : gaps[head];
        internal BeltItem HeadItem => items[head];

        internal BeltItemQueue(int capacity)
        {
            n = capacity;
            gaps = new int[n];
            items = new BeltItem[n];
            blockSizes = new int[n];
        }

        internal bool ContainsIdentity(System.Guid identity)
        {
            for (int i = 0; i < count; i++) if (items[(head + i) % n].Guid == identity) return true;
            return false;
        }

        internal BeltItemState[] CaptureItems()
        {
            // 出口からの累積距離に隙間と幅を変換する。
            // Convert gaps and item widths into distance from the exit.
            var result = new BeltItemState[count];
            int distance = 0;
            for (int i = 0; i < count; i++)
            {
                int p = (head + i) % n;
                distance += gaps[p] + (i == 0 ? 0 : BeltConstants.ItemWidth);
                result[i] = new BeltItemState(items[p], distance);
            }
            return result;
        }

        internal uint ComputeStateHash(uint hash)
        {
            hash = BeltStateHash.Add(hash, count);
            int distance = 0;
            for (int i = 0; i < count; i++)
            {
                int p = (head + i) % n;
                distance += gaps[p] + (i == 0 ? 0 : BeltConstants.ItemWidth);
                hash = BeltStateHash.Add(BeltStateHash.Item(hash, items[p]), distance);
            }
            return hash;
        }

        internal void RestoreItems(BeltItemState[] restoredItems)
        {
            foreach (var state in restoredItems)
                EnqueueTail(state.DistanceToExit - TotalLength, state.Item);
        }

        internal void EnqueueTail(int gap, in BeltItem item)
        {
            // 最後尾に追加し、密着ブロックの両端を更新する。
            // Append at the tail and update both ends of a touching block.
            int p = head + count;
            if (n <= p) p -= n;
            items[p] = item;
            SetPhysicalGap(p, gap);
            if (count == 0 || gap != 0)
            {
                blockSizes[p] = 1;
            }
            else
            {
                int tail = p == 0 ? n - 1 : p - 1;
                int size = blockSizes[tail] + 1;
                int start = p - size + 1;
                if (start < 0) start += n;
                blockSizes[start] = blockSizes[p] = size;
            }
            count++;
        }

        internal void Advance(int tickSpeed, bool sent)
        {
            if (count == 0) return;

            int gapToExit = gaps[head];
            if (sent)
            {
                DequeueHead();
                if (0 < count)
                    SetPhysicalGap(head, gaps[head] - tickSpeed);
                return;
            }
            if (tickSpeed <= gapToExit)
            {
                SetPhysicalGap(head, gapToExit - tickSpeed);
                return;
            }

            int remaining = tickSpeed - gapToExit;
            // 受け入れ拒否: 先頭を出口に置き、ブロック境界の隙間を詰める。
            // On rejection, clamp the head at the exit and close block gaps.
            if (gaps[head] != 0) SetPhysicalGap(head, 0);
            while (blockSizes[head] < count && 0 < remaining)
            {
                int phys = head + blockSizes[head];
                if (n <= phys) phys -= n;
                int gap = gaps[phys];
                if (gap <= remaining)
                {
                    SetPhysicalGap(phys, 0);
                    remaining -= gap;
                    int size = blockSizes[phys];
                    int tail = phys + size - 1;
                    if (n <= tail) tail -= n;
                    blockSizes[head] += size;
                    blockSizes[tail] = blockSizes[head];
                }
                else
                {
                    SetPhysicalGap(phys, gap - remaining);
                    break;
                }
            }
        }

        /// <summary>
        /// 搬出またはbufferへの保存が確定した先頭を取り除き、headと密着数を進める。
        /// 次の先頭のgapを出口までの距離に補正する。残ったアイテムの位置は変えない。
        /// 先頭ブロックの両端に個数を書き戻す。O(1)。
        /// </summary>
        internal void DequeueHead()
        {
            int gapToExit = gaps[head];
            int size = blockSizes[head] - 1;
            items[head] = default;
            SetPhysicalGap(head, 0);
            head++;
            if (head == n) head = 0;
            count--;
            if (0 < count)
                SetPhysicalGap(head, gaps[head] + gapToExit + BeltConstants.ItemWidth);
            if (0 < size)
            {
                int tail = head + size - 1;
                if (n <= tail) tail -= n;
                blockSizes[head] = blockSizes[tail] = size;
            }
        }

        void SetPhysicalGap(int phys, int value)
        {
            // totalGapはgapの合計。挿入位置とLengthの上限により、占有長はint内に収まる。
            // totalGap tracks gap sum; insertion and length bounds keep occupancy within int.
            totalGap = totalGap - gaps[phys] + value;
            gaps[phys] = value;
        }
    }
}
