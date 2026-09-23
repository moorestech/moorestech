using Game.BeltSegment;

namespace Client.Game.InGame.BeltSegment.Gpu
{
    internal sealed class GpuBeltInitialState
    {
        internal readonly GpuBeltState[] States;
        internal readonly GpuBeltBufferState[] Buffers;
        internal readonly int[] Gaps;
        internal readonly int[] Blocks;
        internal readonly GpuBeltItem[] Items;
        internal readonly int[] Speeds;

        internal GpuBeltInitialState(BeltReplaySnapshot snapshot, GpuBeltLayout layout)
        {
            int segmentCount = snapshot.Segments.Length;
            States = new GpuBeltState[segmentCount];
            Buffers = new GpuBeltBufferState[segmentCount];
            Gaps = new int[layout.TotalCapacity];
            Blocks = new int[layout.TotalCapacity];
            Items = new GpuBeltItem[layout.TotalCapacity];
            Speeds = new int[segmentCount];

            for (int segmentId = 0; segmentId < segmentCount; segmentId++)
            {
                var segment = snapshot.Segments[segmentId];
                int offset = layout.Topology[segmentId].Offset;
                int previousDistance = -BeltConstants.ItemWidth;
                int blockStart = 0;
                int blockSize = 0;
                int totalGap = 0;

                // Capture距離→head0隙間列。
                // Capture distances become the head-zero gap lane.
                for (int i = 0; i < segment.Items.Length; i++)
                {
                    var item = segment.Items[i];
                    int gap = item.DistanceToExit - previousDistance - BeltConstants.ItemWidth;
                    Gaps[offset + i] = gap;
                    Items[offset + i] = new GpuBeltItem { Kind = item.Item.ItemId, AcceptedInput = (int)item.Item.AcceptedInput };
                    totalGap += gap;

                    // 密着列の両端だけに個数を置く。
                    // Store touching-block sizes only at both ends.
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
