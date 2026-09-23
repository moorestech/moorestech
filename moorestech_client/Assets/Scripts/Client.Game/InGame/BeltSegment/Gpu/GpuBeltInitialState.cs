using Game.BeltSegment;

namespace Client.Game.InGame.BeltSegment.Gpu
{
    internal sealed class GpuBeltInitialState
    {
        internal readonly GpuBeltState[] States;
        internal readonly GpuBeltBufferState[] Buffers;
        internal readonly int[] Gaps;
        internal readonly int[] Blocks;
        internal readonly int[] Items;
        internal readonly int[] Speeds;

        internal GpuBeltInitialState(BeltReplaySnapshot snapshot, GpuBeltLayout layout)
        {
            int segmentCount = snapshot.Segments.Length;
            States = new GpuBeltState[segmentCount];
            Buffers = new GpuBeltBufferState[segmentCount];
            Gaps = new int[layout.TotalCapacity];
            Blocks = new int[layout.TotalCapacity];
            Items = new int[layout.TotalCapacity];
            Speeds = new int[segmentCount];

            for (int segmentId = 0; segmentId < segmentCount; segmentId++)
            {
                var segment = snapshot.Segments[segmentId];
                int offset = layout.Topology[segmentId].Offset;
                int previousDistance = -BeltConstants.ItemWidth;
                int blockStart = 0;
                int blockSize = 0;
                int totalGap = 0;

                // Captureの出口距離からhead 0の隙間列を復元する。
                // Rebuild the head-zero gap lane from captured exit distances.
                for (int i = 0; i < segment.Items.Length; i++)
                {
                    var item = segment.Items[i];
                    int gap = item.DistanceToExit - previousDistance - BeltConstants.ItemWidth;
                    Gaps[offset + i] = gap;
                    Items[offset + i] = item.Item.ItemId;
                    totalGap += gap;

                    // 密着ブロックの先頭と末尾だけに個数を置く。
                    // Store touching block sizes at its first and last slots.
                    if (i == 0 || gap != 0)
                    {
                        blockStart = i;
                        blockSize = 1;
                    }
                    else blockSize++;
                    Blocks[offset + blockStart] = blockSize;
                    Blocks[offset + i] = blockSize;
                    previousDistance = item.DistanceToExit;
                }

                States[segmentId] = new GpuBeltState
                {
                    Head = 0, Count = segment.Items.Length, TotalGap = totalGap,
                    PriorityIndex = segment.Kind == BeltSegmentKind.Merge ? segment.PriorityIndex : 0
                };
                Buffers[segmentId] = new GpuBeltBufferState
                {
                    HasItem = segment.BufferedItem.HasValue ? 1 : 0,
                    ItemKind = segment.BufferedItem.HasValue ? segment.BufferedItem.Value.ItemId : 0,
                    PriorityIndex = segment.Kind == BeltSegmentKind.Branch ? segment.PriorityIndex : 0
                };
                Speeds[segmentId] = segment.Speed;
            }
        }
    }
}
