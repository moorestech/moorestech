using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BeltSegment
{
    internal static class GpuBeltReplayScenario
    {
        internal static void Run(ComputeShader shader, bool serverParallel)
        {
            var empty = Array.Empty<BeltItemState>();
            var initial = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(3, 64, empty),
                BeltReplaySegmentState.Normal(2, 64, empty),
                BeltReplaySegmentState.Merge(64, 0, empty, null),
                BeltReplaySegmentState.Branch(4, 64, 0, empty, null),
                BeltReplaySegmentState.Normal(3, 64, empty),
                BeltReplaySegmentState.Normal(4, 64, new[] { new BeltItemState(Item(9999), 32) })
            }, new[]
            {
                new BeltReplayLink(0, 2, BeltDirection.Front),
                new BeltReplayLink(1, 2, BeltDirection.Right),
                new BeltReplayLink(2, 3, BeltDirection.Front),
                new BeltReplayLink(5, 5, BeltDirection.Front)
            }, new[]
            {
                new BeltReplayInput(0, BeltDirection.Back),
                new BeltReplayInput(1, BeltDirection.Back),
                new BeltReplayInput(2, BeltDirection.Front),
                new BeltReplayInput(4, BeltDirection.Back)
            }, new[]
            {
                new BeltReplayOutput(3, BeltDirection.Right),
                new BeltReplayOutput(3, BeltDirection.Left),
                new BeltReplayOutput(4, BeltDirection.Front)
            });
            var sources = Enumerable.Range(0, initial.Inputs.Length).Select(_ => new Source()).ToArray();
            var receivers = Enumerable.Range(0, initial.Outputs.Length).Select(_ => new Receiver()).ToArray();
            var actual = new BeltSimulationGraph(initial, sources, receivers);
            var replay = new BeltReplaySimulation(initial);
            var owned = new HashSet<Guid> { Item(9999).Guid };
            var gpu = new GpuBeltSimulation(initial, shader);
            int nextId = 1;
            try
            {
                for (int tick = 0; tick < 1000; tick++)
                {
                    var ready = new List<int>();
                    var successful = new List<int>();
                    var insertions = new List<BeltReplayInsertion>();
                    var speeds = new List<BeltReplaySpeedChange>();
                    for (int i = 0; i < sources.Length; i++)
                    {
                        sources[i].Ready = (tick + i) % 3 != 0;
                        if (sources[i].Ready) ready.Add(i);
                    }
                    for (int i = 0; i < receivers.Length; i++)
                        receivers[i].Prepare((tick + 3 * i) % 7 < 4);
                    for (int i = 0; i < actual.SegmentCount; i++)
                    {
                        int speed = ((tick / 11 + i) % 3) * 64;
                        if (actual.GetSpeed(i) == speed) continue;
                        actual.SetSpeed(i, speed);
                        speeds.Add(new BeltReplaySpeedChange(i, speed));
                    }
                    actual.Tick(serverParallel);
                    for (int i = 0; i < receivers.Length; i++)
                    {
                        if (!receivers[i].Sent) continue;
                        successful.Add(i);
                        Assert.That(owned.Remove(receivers[i].Item.Guid), Is.True, $"output {i}, tick {tick}");
                    }
                    for (int i = 0; i < sources.Length; i++)
                    {
                        if ((tick + i) % 4 == 0) continue;
                        int length = Math.Min(128, actual.GetInputOffer(i));
                        if (length <= 0) continue;
                        var item = Item(nextId);
                        Assert.That(actual.TryInsert(i, length, item), Is.True, $"input {i}, tick {tick}");
                        nextId++;
                        Assert.That(owned.Add(item.Guid), Is.True);
                        insertions.Add(new BeltReplayInsertion(i, length, item));
                    }
                    var frame = new BeltReplayTick(speeds.ToArray(), ready.ToArray(),
                        successful.ToArray(), insertions.ToArray());
                    replay.ApplyTick(frame, !serverParallel);
                    gpu.ApplyTick(frame);
                    var expected = actual.CaptureSnapshot();
                    var reproduced = replay.CaptureSnapshot();
                    AssertSame(expected, reproduced, tick);
                    GpuBeltReplayReadback.AssertMatches(expected, gpu, tick);
                    var ids = reproduced.Segments.SelectMany(s => s.Items.Select(x => x.Item.Guid)
                        .Concat(s.BufferedItem.HasValue ? new[] { s.BufferedItem.Value.Guid } : Array.Empty<Guid>()));
                    Assert.That(ids, Is.EquivalentTo(owned), $"ownership tick {tick}");
                    if ((tick + 1) % 73 != 0) continue;
                    gpu.Dispose();
                    gpu = new GpuBeltSimulation(expected, shader);
                    replay = new BeltReplaySimulation(reproduced);
                    GpuBeltReplayReadback.AssertMatches(expected, gpu, tick);
                }
            }
            finally { gpu.Dispose(); }

            #region Internal

            static BeltItem Item(int id) => new BeltItem
            {
                Guid = new Guid(id, 0, 0, new byte[8]), ItemId = id
            };

            static void AssertSame(BeltReplaySnapshot expected, BeltReplaySnapshot actual, int tick)
            {
                for (int i = 0; i < expected.Segments.Length; i++)
                {
                    var a = expected.Segments[i];
                    var b = actual.Segments[i];
                    Assert.That((b.Kind, b.Capacity, b.Speed, b.PriorityIndex),
                        Is.EqualTo((a.Kind, a.Capacity, a.Speed, a.PriorityIndex)), $"tick {tick}, segment {i}");
                    Assert.That(b.Items.Length, Is.EqualTo(a.Items.Length), $"tick {tick}, segment {i}");
                    for (int j = 0; j < a.Items.Length; j++)
                    {
                        Assert.That(b.Items[j].DistanceToExit, Is.EqualTo(a.Items[j].DistanceToExit));
                        Assert.That(b.Items[j].Item.Guid, Is.EqualTo(a.Items[j].Item.Guid));
                        Assert.That(b.Items[j].Item.ItemId, Is.EqualTo(a.Items[j].Item.ItemId));
                    }
                    Assert.That(b.BufferedItem.HasValue, Is.EqualTo(a.BufferedItem.HasValue));
                    if (a.BufferedItem.HasValue)
                        Assert.That(b.BufferedItem.Value.Guid, Is.EqualTo(a.BufferedItem.Value.Guid));
                }
            }

            #endregion
        }

        sealed class Source : IBeltSource
        {
            internal bool Ready;
            public bool TryGetOutput(BeltDirection direction) => Ready;
        }

        sealed class Receiver : IBeltReceiver
        {
            bool accept;
            internal bool Sent { get; private set; }
            internal BeltItem Item { get; private set; }
            internal void Prepare(bool value) { accept = value; Sent = false; }
            public void AttachInput(IBeltSource source, BeltDirection direction) { }
            public int GetOffer(BeltDirection direction) => BeltConstants.ItemWidth;
            public bool TryReceive(BeltDirection direction, int length, in BeltItem item)
            {
                if (!accept) return false;
                Assert.That(Sent, Is.False);
                Sent = true;
                Item = item;
                return true;
            }
        }
    }
}
