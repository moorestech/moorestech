// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation; see LICENSE.txt.
namespace Core.BeltTransport
{
    // 隙間と密着ブロックをリング配列で保持する。
    // Store gaps and contiguous blocks in a ring buffer.
    internal sealed class BeltItemQueue
    {
        private readonly int n;
        private readonly int[] gaps, blockSizes;
        private readonly BeltItem[] items;
        private int head, count, totalGap;
        internal int Count => count;
        internal int TotalLength => totalGap + BeltConstants.ItemWidth * count;
        internal int HeadGap => gaps[head];
        internal BeltItem HeadItem => items[head];

        internal BeltItemQueue(int capacity)
        {
            n = capacity;
            gaps = new int[n];
            blockSizes = new int[n];
            items = new BeltItem[n];
        }

        internal BeltItemState[] CaptureItems()
        {
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

        internal void EnqueueTail(int gap, in BeltItem item)
        {
            int p = head + count;
            if (n <= p) p -= n;
            items[p] = item;
            SetPhysicalGap(p, gap);
            // 密着した末尾を既存ブロックへ結合する。
            // Join a touching tail to its contiguous block.
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

        internal void Advance(bool sent, int tickSpeed)
        {
            if (count == 0) return;

            int gapToExit = gaps[head];
            // 搬出成功時は後続も同じ速度で前進する。
            // Advance followers at the same speed after a successful output.
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
            // 出口で先頭を止め、後続の隙間を詰める。
            // Clamp the head and close the following block gaps.
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
                    // 閉じた隙間の両側のブロックを結合する。
                    // Merge contiguous blocks across the closed gap.
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

        internal void DequeueHead()
        {
            int gapToExit = gaps[head];
            int size = blockSizes[head] - 1;
            items[head] = default;
            SetPhysicalGap(head, 0);
            head++;
            if (head == n) head = 0;
            count--;
            // 先頭削除だけでは後続の絶対位置を変えない。
            // Preserve follower positions when removing only the head.
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
            // 差分更新で占有長を一定時間で求める。
            // Maintain total occupied length incrementally.
            totalGap = totalGap - gaps[phys] + value;
            gaps[phys] = value;
        }
    }
}
