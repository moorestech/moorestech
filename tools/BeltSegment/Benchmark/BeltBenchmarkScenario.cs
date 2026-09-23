using System;
using System.Collections.Generic;
using Game.BeltSegment;

namespace BeltSegment.Benchmark;

internal sealed class BeltBenchmarkScenario
{
    private const int SeedIntervalCells = 4;
    private readonly BeltConveyorSegment[] segments;
    private readonly BenchmarkSink[] sinks;
    private readonly HashSet<Guid> initialGuids;

    internal BeltSimulation Simulation { get; }
    internal int InitialItemCount => initialGuids.Count;
    internal long OutputCount
    {
        get
        {
            long count = 0;
            foreach (var sink in sinks) count += sink.OutputCount;
            return count;
        }
    }
    internal long ReinsertionCount
    {
        get
        {
            long count = 0;
            foreach (var sink in sinks) count += sink.ReinsertionCount;
            return count;
        }
    }

    internal BeltBenchmarkScenario(int segmentCount, int capacity)
    {
        segments = new BeltConveyorSegment[segmentCount];
        sinks = new BenchmarkSink[segmentCount];
        initialGuids = new HashSet<Guid>();

        // 4マス間隔で配置しsinkは個別。
        // Seed every fourth cell with a dedicated sink.
        for (var index = 0; index < segmentCount; index++)
        {
            var segment = new BeltConveyorSegment(capacity, 32, BeltSegmentKind.Normal, 0);
            var sink = new BenchmarkSink();
            segment.ConnectTo(sink, BeltDirection.Front);
            var states = new BeltItemState[(capacity + SeedIntervalCells - 1) / SeedIntervalCells];
            for (var itemIndex = 0; itemIndex < states.Length; itemIndex++)
            {
                var cellIndex = itemIndex * SeedIntervalCells;
                var item = new BeltItem
                {
                    Guid = Guid.NewGuid(),
                    ItemId = 1,
                    Position = new ItemPosition(new BeltCell(index, cellIndex, 0), BeltEntryDirection.FromBack, 256)
                };
                initialGuids.Add(item.Guid);
                states[itemIndex] = new BeltItemState(item, cellIndex * BeltConstants.ItemWidth);
            }
            segment.RestoreItems(states);
            segments[index] = segment;
            sinks[index] = sink;
        }
        Simulation = new BeltSimulation(segments);
    }

    internal void ReinsertOutputs()
    {
        for (var index = 0; index < segments.Length; index++)
        {
            sinks[index].Reinsert(segments[index]);
        }
    }

    internal bool ValidateItems(out int finalItemCount, out string error)
    {
        if (OutputCount == 0)
        {
            finalItemCount = 0;
            error = "No output was transferred during the measured ticks.";
            return false;
        }
        var finalGuids = new HashSet<Guid>();
        finalItemCount = 0;
        for (var index = 0; index < segments.Length; index++)
        {
            foreach (var state in segments[index].CaptureItems())
            {
                finalItemCount++;
                if (!finalGuids.Add(state.Item.Guid))
                {
                    error = $"Duplicate GUID in segment {index}: {state.Item.Guid}";
                    return false;
                }
            }
            if (!sinks[index].HasPending) continue;
            finalItemCount++;
            if (!finalGuids.Add(sinks[index].PendingGuid))
            {
                error = $"Duplicate GUID in sink {index}: {sinks[index].PendingGuid}";
                return false;
            }
        }

        if (finalItemCount != InitialItemCount || !initialGuids.SetEquals(finalGuids))
        {
            error = $"Item conservation failed: initial={InitialItemCount}, final={finalItemCount}.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private sealed class BenchmarkSink : IBeltReceiver
    {
        private BeltItem pendingItem;
        private int pendingLength;
        private bool hasPending;
        private long outputCount;

        internal bool HasPending => hasPending;
        internal Guid PendingGuid => pendingItem.Guid;
        internal long OutputCount => outputCount;
        internal long ReinsertionCount { get; private set; }

        public void AttachInput(IBeltSource source, BeltDirection inputDirection) { }
        public int GetOffer(BeltDirection inputDirection) => BeltConstants.ItemWidth;

        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            if (hasPending) return false;
            pendingItem = item;
            pendingLength = length;
            hasPending = true;
            outputCount++;
            return true;
        }

        internal void Reinsert(BeltConveyorSegment segment)
        {
            if (!hasPending || !segment.TryReceive(BeltDirection.Back, pendingLength, pendingItem)) return;
            hasPending = false;
            pendingItem = default;
            pendingLength = 0;
            ReinsertionCount++;
        }

    }
}
