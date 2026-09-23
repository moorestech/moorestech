using System;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using NUnit.Framework;
using static Client.Tests.BeltSegment.GpuBeltReplayTest;

namespace Client.Tests.BeltSegment
{
    public sealed class GpuBeltReplayBoundaryTest
    {
        static readonly BeltItemState[] empty = Array.Empty<BeltItemState>();

        [TestCase(false)]
        [TestCase(true)]
        public void BufferedItemMovesThenNormalAdvancesInSameTick(bool merge)
        {
            var source = merge
                ? BeltReplaySegmentState.Merge(64, 0, empty, Item(0))
                : BeltReplaySegmentState.Branch(2, 64, 0, empty, Item(0));
            var snapshot = new BeltReplaySnapshot(new[]
            {
                source, BeltReplaySegmentState.Normal(2, 64, empty)
            }, new[] { new BeltReplayLink(0, 1, BeltDirection.Front) },
                Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, noIds, noInsertions));
                Assert.That(cpu.CaptureSnapshot().Segments[1].Items[0].DistanceToExit, Is.EqualTo(384));
                Assert.That(cpu.CaptureSnapshot().Segments[1].Items[0].Item.ItemId, Is.Zero);
            }
        }

        [Test]
        public void ZeroSpeedStillCollectsButDoesNotTransfer()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Branch(2, 0, 0,
                    new[] { GpuBeltReplayTest.State(0, 0) }, null)
            }, Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(),
                new[] { new BeltReplayOutput(0, BeltDirection.Front) });
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, noIds, noInsertions));
                var state = cpu.CaptureSnapshot().Segments[0];
                Assert.That(state.Items, Is.Empty);
                Assert.That(state.BufferedItem.HasValue, Is.True);
                Assert.That(state.BufferedItem.Value.ItemId, Is.Zero);
            }
        }

        [Test]
        public void BranchRotatesFromStartIndexEvenWhenLaterOutputAccepts()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Branch(1, 64, 0, empty, Item(27))
            }, Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(), new[]
            {
                new BeltReplayOutput(0, BeltDirection.Front),
                new BeltReplayOutput(0, BeltDirection.Left),
                new BeltReplayOutput(0, BeltDirection.Right)
            });
            var first = new Receiver(false);
            var second = new Receiver(true);
            var third = new Receiver(true);
            var actual = new BeltSimulationGraph(snapshot, Array.Empty<IBeltSource>(),
                new IBeltReceiver[] { first, second, third });
            actual.Tick(false);
            Assert.That((first.Sent, second.Sent, third.Sent), Is.EqualTo((false, true, false)));
            Assert.That(actual.CaptureSnapshot().Segments[0].PriorityIndex, Is.EqualTo(1));
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, new[] { 1 }, noInsertions));
                Assert.That(cpu.CaptureSnapshot().Segments[0].BufferedItem.HasValue, Is.False);
                Assert.That(cpu.CaptureSnapshot().Segments[0].PriorityIndex, Is.EqualTo(1));
            }
        }

        [TestCase(0, 31)]
        [TestCase(1, 17)]
        public void MergeReadyInputsUseRegistrationOrderAndInitialPriority(int priority, int expectedKind)
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(1, 128, new[] { GpuBeltReplayTest.State(17, 0) }),
                BeltReplaySegmentState.Normal(1, 128, new[] { GpuBeltReplayTest.State(31, 0) }),
                BeltReplaySegmentState.Merge(0, priority, empty, null)
            }, new[]
            {
                new BeltReplayLink(1, 2, BeltDirection.Front),
                new BeltReplayLink(0, 2, BeltDirection.Right)
            }, new[] { new BeltReplayInput(2, BeltDirection.Front) }, Array.Empty<BeltReplayOutput>());
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(new[] { 0, 0 }, noIds, noInsertions));
                Assert.That(cpu.CaptureSnapshot().Segments[2].Items[0].Item.ItemId, Is.EqualTo(expectedKind));
                Assert.That(cpu.CaptureSnapshot().Segments[2].Items[0].DistanceToExit, Is.EqualTo(128));
            }
        }

        [Test]
        public void ExternalSuccessAndDuplicatePositiveIdsResetEachTick()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(2, 128, new[]
                {
                    GpuBeltReplayTest.State(1, 0), GpuBeltReplayTest.State(2, 256)
                })
            }, Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(),
                new[] { new BeltReplayOutput(0, BeltDirection.Front) });
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, new[] { 0, 0 }, noInsertions));
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, noIds, noInsertions));
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, noIds, noInsertions));
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items.Length, Is.EqualTo(1));
                Assert.That(cpu.CaptureSnapshot().Segments[0].Items[0].Item.ItemId, Is.EqualTo(2));
            }
        }

        [Test]
        public void LastPartialDispatchGroupKeepsAll129Segments()
        {
            var segments = new BeltReplaySegmentState[129];
            for (int i = 0; i < segments.Length; i++)
                segments[i] = BeltReplaySegmentState.Normal(1, i == 128 ? 64 : 0, i == 128
                    ? new[] { GpuBeltReplayTest.State(128, 128) } : empty);
            var snapshot = GpuBeltReplayTest.Snapshot(segments);
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, noIds, noInsertions));
                var last = cpu.CaptureSnapshot().Segments[128];
                Assert.That(last.Items.Length, Is.EqualTo(1));
                Assert.That(last.Items[0].Item.ItemId, Is.EqualTo(128));
                Assert.That(last.Items[0].DistanceToExit, Is.EqualTo(64));
                GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, noIds, noInsertions));
                last = cpu.CaptureSnapshot().Segments[128];
                Assert.That(last.Items.Length, Is.EqualTo(1));
                Assert.That(last.Items[0].Item.ItemId, Is.EqualTo(128));
                Assert.That(last.Items[0].DistanceToExit, Is.Zero);
            }
        }

        [Test]
        public void TwoNormalCycleTransfersBothItemsWithoutLoss()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(2, 64, new[] { GpuBeltReplayTest.State(11, 32) }),
                BeltReplaySegmentState.Normal(2, 64, new[] { GpuBeltReplayTest.State(22, 32) })
            }, new[]
            {
                new BeltReplayLink(0, 1, BeltDirection.Front),
                new BeltReplayLink(1, 0, BeltDirection.Right)
            }, Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>());
            using (var gpu = new GpuBeltSimulation(snapshot, GpuBeltReplayTest.Shader()))
            {
                var cpu = new BeltReplaySimulation(snapshot);
                for (int tick = 0; tick < 18; tick++)
                {
                    GpuBeltReplayTest.Apply(cpu, gpu, GpuBeltReplayTest.Tick(noIds, noIds, noInsertions));
                    var segments = cpu.CaptureSnapshot().Segments;
                    Assert.That(segments[0].Items.Length, Is.EqualTo(1), $"tick {tick}");
                    Assert.That(segments[1].Items.Length, Is.EqualTo(1), $"tick {tick}");
                    Assert.That(new[] { segments[0].Items[0].Item.ItemId, segments[1].Items[0].Item.ItemId },
                        Is.EquivalentTo(new[] { 11, 22 }), $"tick {tick} conservation");
                    if (tick == 0 || tick == 8)
                    {
                        Assert.That(segments[0].Items[0].DistanceToExit, Is.EqualTo(480));
                        Assert.That(segments[1].Items[0].DistanceToExit, Is.EqualTo(480));
                    }
                }
            }
        }

        static BeltItem Item(int kind) => new BeltItem { ItemId = kind };

        sealed class Receiver : IBeltReceiver
        {
            readonly bool accept;
            internal bool Sent { get; private set; }
            internal Receiver(bool accept) => this.accept = accept;
            public void AttachInput(IBeltSource source, BeltDirection direction) { }
            public int GetOffer(BeltDirection direction) => BeltConstants.ItemWidth;
            public bool TryReceive(BeltDirection direction, int length, in BeltItem item)
            {
                Sent = accept;
                return accept;
            }
        }
    }
}
