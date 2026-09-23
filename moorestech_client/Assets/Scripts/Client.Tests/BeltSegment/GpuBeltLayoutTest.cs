using System;
using System.Runtime.InteropServices;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using NUnit.Framework;

namespace Client.Tests.BeltSegment
{
    public sealed class GpuBeltLayoutTest
    {
        [Test]
        public void AbiMatchesIntegerStridesAndCoreEnums()
        {
            // HLSLが読むstrideと判別値を固定する。
            // Fix the strides and discriminants read by HLSL.
            Assert.That(Marshal.SizeOf<GpuBeltTopology>(), Is.EqualTo(32));
            Assert.That(Marshal.SizeOf<GpuBeltPort>(), Is.EqualTo(16));
            Assert.That(Marshal.SizeOf<GpuBeltState>(), Is.EqualTo(16));
            Assert.That(Marshal.SizeOf<GpuBeltBufferState>(), Is.EqualTo(16));
            Assert.That(Marshal.SizeOf<GpuBeltNormalLink>(), Is.EqualTo(16));
            Assert.That(Marshal.SizeOf<GpuBeltNormalState>(), Is.EqualTo(16));
            Assert.That(Marshal.SizeOf<GpuBeltEvent>(), Is.EqualTo(16));
            Assert.That((int)BeltSegmentKind.Normal, Is.Zero);
            Assert.That((int)BeltSegmentKind.Merge, Is.EqualTo(1));
            Assert.That((int)BeltSegmentKind.Branch, Is.EqualTo(2));
            Assert.That((int)BeltDirection.None, Is.EqualTo(-1));
            Assert.That((int)BeltDirection.Front, Is.Zero);
            Assert.That((int)BeltDirection.Back, Is.EqualTo(1));
            Assert.That((int)BeltDirection.Left, Is.EqualTo(2));
            Assert.That((int)BeltDirection.Right, Is.EqualTo(3));
            Assert.That((GpuBeltData.PortSegment, GpuBeltData.PortBuffer, GpuBeltData.PortExternal),
                Is.EqualTo((0, 1, 2)));
            Assert.That((GpuBeltData.ReadyInput, GpuBeltData.SuccessfulOutput, GpuBeltData.Speed, GpuBeltData.Insertion),
                Is.EqualTo((0, 1, 2, 3)));
        }

        [Test]
        public void EmptyGraphHasNoLogicalEntries()
        {
            var snapshot = Snapshot(Array.Empty<BeltReplaySegmentState>());
            var layout = new GpuBeltLayout(snapshot);
            var initial = new GpuBeltInitialState(snapshot, layout);
            Assert.That(layout.Topology, Is.Empty);
            Assert.That(layout.InputPorts, Is.Empty);
            Assert.That(layout.OutputPorts, Is.Empty);
            Assert.That(layout.ExternalInputs, Is.Empty);
            Assert.That(layout.NormalLinks, Is.Empty);
            Assert.That((layout.TotalCapacity, layout.OutputCount), Is.EqualTo((0, 0)));
            Assert.That(initial.States, Is.Empty);
            Assert.That(initial.Buffers, Is.Empty);
            Assert.That(initial.Gaps, Is.Empty);
            Assert.That(initial.Blocks, Is.Empty);
            Assert.That(initial.Items, Is.Empty);
            Assert.That(initial.Speeds, Is.Empty);
        }

        [Test]
        public void QueuePreservesZeroKindDistancesAndSeparateBufferOccupancy()
        {
            var snapshot = Snapshot(new[]
            {
                BeltReplaySegmentState.Normal(4, 64, new[] { State(0, 32), State(7, 288), State(9, 800) }),
                BeltReplaySegmentState.Merge(80, 2, Array.Empty<BeltItemState>(), new BeltItem { ItemId = 0 }),
                BeltReplaySegmentState.Branch(2, 96, 1, Array.Empty<BeltItemState>(), null)
            });
            var layout = new GpuBeltLayout(snapshot);
            var initial = new GpuBeltInitialState(snapshot, layout);

            Assert.That(layout.Topology[0].Offset, Is.Zero);
            Assert.That(layout.Topology[1].Offset, Is.EqualTo(4));
            Assert.That(layout.Topology[2].Offset, Is.EqualTo(5));
            Assert.That(layout.TotalCapacity, Is.EqualTo(7));
            Assert.That(initial.States[0].Head, Is.Zero);
            Assert.That(initial.States[0].Count, Is.EqualTo(3));
            Assert.That(initial.States[0].TotalGap, Is.EqualTo(288));
            Assert.That(initial.Gaps, Is.EqualTo(new[] { 32, 0, 256, 0, 0, 0, 0 }));
            Assert.That(initial.Blocks, Is.EqualTo(new[] { 2, 2, 1, 0, 0, 0, 0 }));
            Assert.That(initial.Items, Is.EqualTo(new[] { 0, 7, 9, 0, 0, 0, 0 }));
            Assert.That(initial.Buffers[1].HasItem, Is.EqualTo(1));
            Assert.That(initial.Buffers[1].ItemKind, Is.Zero);
            Assert.That(initial.Buffers[2].HasItem, Is.Zero);
            Assert.That(initial.States[1].PriorityIndex, Is.EqualTo(2));
            Assert.That(initial.Buffers[2].PriorityIndex, Is.EqualTo(1));
            Assert.That(initial.Speeds, Is.EqualTo(new[] { 64, 80, 96 }));
            Assert.That(snapshot.Segments[0].Items[0].DistanceToExit, Is.EqualTo(32));
        }

        [Test]
        public void PortsPreserveCoreRegistrationOrderAcrossSegments()
        {
            var empty = Array.Empty<BeltItemState>();
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(1, 64, empty), BeltReplaySegmentState.Normal(1, 64, empty),
                BeltReplaySegmentState.Merge(64, 1, empty, null), BeltReplaySegmentState.Branch(2, 64, 1, empty, null),
                BeltReplaySegmentState.Normal(1, 64, empty)
            }, new[]
            {
                new BeltReplayLink(1, 2, BeltDirection.Right),
                new BeltReplayLink(0, 2, BeltDirection.Front),
                new BeltReplayLink(3, 4, BeltDirection.Left)
            }, new[] { new BeltReplayInput(2, BeltDirection.Left), new BeltReplayInput(4, BeltDirection.Back) },
                new[] { new BeltReplayOutput(3, BeltDirection.Right), new BeltReplayOutput(4, BeltDirection.Front) });
            var layout = new GpuBeltLayout(snapshot);

            // 合流には逆ID順の内部接続の後で外部入力が並ぶ。
            // The merge sees reverse-ID links before its external input.
            Assert.That(layout.Topology[2].FirstInput, Is.Zero);
            Assert.That(layout.Topology[2].InputCount, Is.EqualTo(3));
            AssertPort(layout.InputPorts[0], 0, 1, 2);
            AssertPort(layout.InputPorts[1], 0, 0, 1);
            AssertPort(layout.InputPorts[2], 2, 0, 2);
            Assert.That(layout.Topology[4].FirstInput, Is.EqualTo(3));
            AssertPort(layout.InputPorts[3], 1, 3, 3);
            AssertPort(layout.InputPorts[4], 2, 1, 1);

            // 分岐のlink出力は機械出力より先に登録される。
            // The branch link output precedes its machine output.
            Assert.That(layout.Topology[3].FirstOutput, Is.EqualTo(2));
            AssertPort(layout.OutputPorts[2], 0, 4, 2);
            AssertPort(layout.OutputPorts[3], 2, 0, 3);
            AssertPort(layout.OutputPorts[4], 2, 1, 0);
            AssertPort(layout.ExternalInputs[0], 0, 2, 2);
            AssertPort(layout.ExternalInputs[1], 0, 4, 1);
            Assert.That(layout.OutputCount, Is.EqualTo(2));
            Assert.That(layout.NormalLinks, Is.Empty);
            Assert.That(layout.Topology[0].NormalLinkIndex, Is.EqualTo(-1));
        }

        [TestCase(BeltDirection.Front, 1)]
        [TestCase(BeltDirection.Back, 0)]
        [TestCase(BeltDirection.Left, 3)]
        [TestCase(BeltDirection.Right, 2)]
        public void EveryLinkDirectionPacksItsOpposite(BeltDirection outputDirection, int inputDirection)
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(1, 64, Array.Empty<BeltItemState>()),
                BeltReplaySegmentState.Normal(1, 64, Array.Empty<BeltItemState>())
            }, new[] { new BeltReplayLink(0, 1, outputDirection) }, Array.Empty<BeltReplayInput>(),
                Array.Empty<BeltReplayOutput>());
            var layout = new GpuBeltLayout(snapshot);
            AssertPort(layout.OutputPorts[0], 0, 1, (int)outputDirection);
            AssertPort(layout.InputPorts[0], 0, 0, inputDirection);
            Assert.That(layout.NormalLinks[0].InputDirection, Is.EqualTo(inputDirection));
            Assert.That(layout.Topology[0].NormalLinkIndex, Is.Zero);
        }

        [Test]
        public void NormalSelfLinkIsExtractedOnce()
        {
            var snapshot = new BeltReplaySnapshot(new[]
            {
                BeltReplaySegmentState.Normal(2, 64, Array.Empty<BeltItemState>())
            }, new[] { new BeltReplayLink(0, 0, BeltDirection.Front) }, Array.Empty<BeltReplayInput>(),
                Array.Empty<BeltReplayOutput>());
            var layout = new GpuBeltLayout(snapshot);
            Assert.That(layout.NormalLinks.Length, Is.EqualTo(1));
            Assert.That((layout.NormalLinks[0].Source, layout.NormalLinks[0].Target,
                layout.NormalLinks[0].InputDirection), Is.EqualTo((0, 0, 1)));
            Assert.That(layout.Topology[0].NormalLinkIndex, Is.Zero);
            Assert.That((layout.Topology[0].InputCount, layout.Topology[0].OutputCount), Is.EqualTo((1, 1)));
        }

        private static BeltReplaySnapshot Snapshot(BeltReplaySegmentState[] segments)
            => new BeltReplaySnapshot(segments, Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(),
                Array.Empty<BeltReplayOutput>());

        private static BeltItemState State(int kind, int distance)
            => new BeltItemState(new BeltItem { ItemId = kind }, distance);

        private static void AssertPort(GpuBeltPort port, int kind, int id, int direction)
            => Assert.That((port.Kind, port.Id, port.Direction), Is.EqualTo((kind, id, direction)));
    }
}
