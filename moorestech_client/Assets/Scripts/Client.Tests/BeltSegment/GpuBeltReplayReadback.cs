using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BeltSegment
{
    internal static class GpuBeltReplayReadback
    {
        internal static void AssertMatches(BeltReplaySnapshot expected, GpuBeltSimulation simulation, int tick)
        {
            var layout = new GpuBeltLayout(expected);
            var buffers = simulation.Buffers;
            var states = Read<GpuBeltState>(buffers.States, expected.Segments.Length);
            var occupants = Read<GpuBeltBufferState>(buffers.Buffers, expected.Segments.Length);
            var gaps = Read<int>(buffers.Gaps, layout.TotalCapacity);
            var blocks = Read<int>(buffers.Blocks, layout.TotalCapacity);
            var kinds = Read<GpuBeltItem>(buffers.Items, layout.TotalCapacity);
            var speeds = Read<int>(buffers.Speeds, expected.Segments.Length);
            for (int id = 0; id < expected.Segments.Length; id++)
            {
                var segment = expected.Segments[id];
                var topology = layout.Topology[id];
                var state = states[id];
                var buffer = occupants[id];
                if (state.Count != segment.Items.Length)
                    Assert.Fail($"tick {tick} segment {id}: GPU head={state.Head} count={state.Count} gap={state.TotalGap} speed={speeds[id]} buffer={buffer.HasItem}/{buffer.ItemKind}; CPU count={segment.Items.Length} items={string.Join(",", System.Array.ConvertAll(segment.Items, x => $"{x.Item.ItemId}@{x.DistanceToExit}"))} buffer={segment.BufferedItem.HasValue}; gaps={string.Join(",", gaps)} blocks={string.Join(",", blocks)} kinds={string.Join(",", kinds)}");
                Assert.That(speeds[id], Is.EqualTo(segment.Speed), $"segment {id} speed");
                Assert.That(state.Count, Is.EqualTo(segment.Items.Length), $"tick {tick}, segment {id} count");
                Assert.That(buffer.HasItem, Is.EqualTo(segment.BufferedItem.HasValue ? 1 : 0), $"segment {id} buffer");
                if (segment.BufferedItem.HasValue)
                    Assert.That(buffer.ItemKind, Is.EqualTo(segment.BufferedItem.Value.ItemId), $"segment {id} buffer kind");
                int priority = segment.Kind == BeltSegmentKind.Merge ? state.PriorityIndex
                    : segment.Kind == BeltSegmentKind.Branch ? buffer.PriorityIndex : 0;
                Assert.That(priority, Is.EqualTo(segment.PriorityIndex), $"segment {id} priority");
                int distance = 0;
                int totalGap = 0;
                for (int item = 0; item < state.Count; item++)
                {
                    int p = topology.Offset + (state.Head + item) % topology.Capacity;
                    distance += gaps[p] + (item == 0 ? 0 : BeltConstants.ItemWidth);
                    totalGap += gaps[p];
                    Assert.That(distance, Is.EqualTo(segment.Items[item].DistanceToExit), $"segment {id} item {item} distance");
                    Assert.That(kinds[p].Kind, Is.EqualTo(segment.Items[item].Item.ItemId), $"segment {id} item {item} kind");
                    Assert.That(kinds[p].AcceptedInput, Is.EqualTo((int)segment.Items[item].Item.AcceptedInput), $"segment {id} item {item} entry");
                    if (item == 0 || gaps[p] != 0)
                    {
                        int size = 1;
                        while (item + size < state.Count)
                        {
                            int next = topology.Offset + (state.Head + item + size) % topology.Capacity;
                            if (gaps[next] != 0) break;
                            size++;
                        }
                        int tail = topology.Offset + (state.Head + item + size - 1) % topology.Capacity;
                        Assert.That(blocks[p], Is.EqualTo(size), $"segment {id} block first {item}");
                        Assert.That(blocks[tail], Is.EqualTo(size), $"segment {id} block last {item}");
                    }
                }
                Assert.That(state.TotalGap, Is.EqualTo(totalGap), $"segment {id} total gap");
            }

            #region Internal

            static T[] Read<T>(GraphicsBuffer buffer, int count) where T : struct
            {
                var values = new T[count];
                if (count != 0) buffer.GetData(values, 0, 0, count);
                return values;
            }

            #endregion
        }
    }
}
