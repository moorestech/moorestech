using System;
using System.Collections.Generic;
using System.Linq;
using Game.BeltSegment;
using NUnit.Framework;

namespace Tests.UnitTest.Game.BeltSegment
{
    internal static class BeltExternalReplayScenario
    {
        internal static BeltItemState State(int id, int distance) => new BeltItemState(new BeltItem
        {
            Guid = new Guid(id, 0, 0, new byte[8]), ItemId = 1 + id % 7,
            Position = new ItemPosition(new BeltCell(id, 2, 3), BeltEntryDirection.FromBackAbove, 37)
        }, distance);

        internal static BeltReplaySnapshot Snapshot(BeltReplaySegmentState[] segments, BeltReplayLink[] links,
            BeltReplayInput[] inputs, BeltReplayOutput[] outputs) => new BeltReplaySnapshot(segments, links, inputs, outputs);

        internal static BeltReplayTick Frame(int[] ready, int[] outputs, params BeltReplayInsertion[] insertions)
            => new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), ready, outputs, insertions);

        internal static void Run(bool serverParallel)
        {
            var empty = Array.Empty<BeltItemState>();
            var initial = Snapshot(new[]
            {
                BeltReplaySegmentState.Normal(3, 64, empty), BeltReplaySegmentState.Normal(2, 64, empty),
                BeltReplaySegmentState.Merge(64, 0, empty, null), BeltReplaySegmentState.Branch(4, 64, 0, empty, null),
                BeltReplaySegmentState.Normal(3, 64, empty), BeltReplaySegmentState.Normal(4, 64, new[] { State(9999, 32) })
            }, new[]
            {
                new BeltReplayLink(0, 2, BeltDirection.Front), new BeltReplayLink(1, 2, BeltDirection.Right),
                new BeltReplayLink(2, 3, BeltDirection.Front), new BeltReplayLink(5, 5, BeltDirection.Front)
            }, new[]
            {
                new BeltReplayInput(0, BeltDirection.Back), new BeltReplayInput(1, BeltDirection.Back),
                new BeltReplayInput(2, BeltDirection.Front), new BeltReplayInput(4, BeltDirection.Back)
            }, new[]
            {
                new BeltReplayOutput(3, BeltDirection.Right), new BeltReplayOutput(3, BeltDirection.Left),
                new BeltReplayOutput(4, BeltDirection.Front)
            });
            var sources = Enumerable.Range(0, initial.Inputs.Length).Select(_ => new Source()).ToArray();
            var receivers = Enumerable.Range(0, initial.Outputs.Length).Select(_ => new Receiver()).ToArray();
            var actual = new BeltSimulationGraph(initial, sources, receivers);
            var replay = new BeltReplaySimulation(initial);
            var owned = new HashSet<Guid> { State(9999, 32).Item.Guid };
            int nextId = 1;
            for (int tick = 0; tick < 1000; tick++)
            {
                var ready = new List<int>();
                var successful = new List<int>();
                var insertions = new List<BeltReplayInsertion>();
                var changes = new List<BeltReplaySpeedChange>();
                // 実ポートは供給可否と受入可否を独立して固定する。
                // Fix real supply readiness and receiver acceptance independently.
                for (int i = 0; i < sources.Length; i++)
                {
                    sources[i].SetReady((tick + i) % 3 != 0);
                    if (sources[i].TryGetOutput(initial.Inputs[i].InputDirection)) ready.Add(i);
                }
                for (int i = 0; i < receivers.Length; i++) receivers[i].Prepare((tick + i * 3) % 7 < 4);
                for (int i = 0; i < actual.Segments.Count; i++)
                {
                    int speed = ((tick / 11 + i) % 3) * 64;
                    if (actual.Segments[i].Speed == speed) continue;
                    actual.Segments[i].SetSpeed(speed);
                    changes.Add(new BeltReplaySpeedChange(i, speed));
                }
                actual.Simulation.Tick(serverParallel);
                // 成功した搬出だけを記録し、所有集合から一度だけ除く。
                // Record successful outputs and remove each from ownership exactly once.
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (!receivers[i].Sent) continue;
                    successful.Add(i);
                    Assert.That(owned.Remove(receivers[i].Item.Guid), Is.True, $"output {i}, tick {tick}");
                }
                for (int i = 0; i < sources.Length; i++)
                {
                    if ((tick + i) % 4 == 0) continue;
                    var input = initial.Inputs[i];
                    var target = actual.Segments[input.TargetSegmentId];
                    int length = Math.Min(128, target.GetOffer(input.InputDirection));
                    if (length <= 0) continue;
                    var item = State(nextId, 0).Item;
                    if (!target.TryReceive(input.InputDirection, length, item)) continue;
                    nextId++;
                    Assert.That(owned.Add(item.Guid), Is.True);
                    insertions.Add(new BeltReplayInsertion(i, length, item));
                }
                // 反対の実行方式と周期的な再生成を交えて全状態を比較する。
                // Compare complete state using the opposite execution mode and periodic reconstruction.
                replay.ApplyTick(new BeltReplayTick(changes.ToArray(), ready.ToArray(), successful.ToArray(), insertions.ToArray()), !serverParallel);
                var expected = actual.CaptureSnapshot();
                var reproduced = replay.CaptureSnapshot();
                AssertSame(expected, reproduced);
                var ids = reproduced.Segments.SelectMany(s => s.Items.Select(item => item.Item.Guid)
                    .Concat(s.BufferedItem.HasValue ? new[] { s.BufferedItem.Value.Guid } : Array.Empty<Guid>())).ToArray();
                Assert.That(ids, Is.EquivalentTo(owned), $"ownership tick {tick}");
                if ((tick + 1) % 73 == 0) replay = new BeltReplaySimulation(reproduced);
            }
        }

        internal static void AssertSame(BeltReplaySnapshot expected, BeltReplaySnapshot actual)
        {
            Assert.That(actual.Links, Is.EqualTo(expected.Links));
            Assert.That(actual.Inputs, Is.EqualTo(expected.Inputs));
            Assert.That(actual.Outputs, Is.EqualTo(expected.Outputs));
            Assert.That(actual.Segments.Length, Is.EqualTo(expected.Segments.Length));
            for (int i = 0; i < expected.Segments.Length; i++)
            {
                var left = expected.Segments[i];
                var right = actual.Segments[i];
                Assert.That((right.Kind, right.Capacity, right.Speed, right.PriorityIndex),
                    Is.EqualTo((left.Kind, left.Capacity, left.Speed, left.PriorityIndex)));
                Assert.That(right.Items.Length, Is.EqualTo(left.Items.Length));
                for (int j = 0; j < left.Items.Length; j++)
                {
                    Assert.That(right.Items[j].DistanceToExit, Is.EqualTo(left.Items[j].DistanceToExit));
                    AssertItem(left.Items[j].Item, right.Items[j].Item);
                }
                Assert.That(right.BufferedItem.HasValue, Is.EqualTo(left.BufferedItem.HasValue));
                if (left.BufferedItem.HasValue) AssertItem(left.BufferedItem.Value, right.BufferedItem.Value);
            }
            #region Internal
            void AssertItem(BeltItem left, BeltItem right)
            {
                Assert.That(right.Guid, Is.EqualTo(left.Guid));
                Assert.That(right.ItemId, Is.EqualTo(left.ItemId));
                Assert.That(right.Position.CurrentCell, Is.EqualTo(left.Position.CurrentCell));
                Assert.That(right.Position.EntryDirection, Is.EqualTo(left.Position.EntryDirection));
                Assert.That(right.Position.Progress, Is.EqualTo(left.Position.Progress));
                Assert.That(right.Position.Position, Is.EqualTo(left.Position.Position));
            }
            #endregion
        }

        internal sealed class Source : IBeltSource
        {
            private bool ready;
            internal void SetReady(bool value) => ready = value;
            public bool TryGetOutput(BeltDirection inputDirection) => ready;
        }

        internal sealed class Receiver : IBeltReceiver
        {
            private bool canAccept;
            internal bool Sent { get; private set; }
            internal BeltItem Item { get; private set; }
            internal void Prepare(bool value)
            {
                canAccept = value;
                Sent = false;
                Item = default;
            }
            public void AttachInput(IBeltSource source, BeltDirection inputDirection) { }
            // offerが正でも実搬入で拒否する機械を再現元にする。
            // The real machine may reject an insertion despite a positive offer.
            public int GetOffer(BeltDirection inputDirection) => BeltConstants.ItemWidth;
            public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
            {
                if (!canAccept) return false;
                Assert.That(Sent, Is.False);
                Sent = true;
                Item = item;
                return true;
            }
        }
    }
}
