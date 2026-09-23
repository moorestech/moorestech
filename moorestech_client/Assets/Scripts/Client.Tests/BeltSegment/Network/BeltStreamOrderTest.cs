using System;
using System.Text.RegularExpressions;
using Client.Game.InGame.BeltSegment.Model;
using Game.BeltSegment;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.BeltSegment.Network.BeltNetworkFixture;
namespace Client.Tests.BeltSegment.Network
{
    public sealed class BeltStreamOrderTest
    {
        [Test]
        public void ReversedFramesDrainExactlyOnceAfterSnapshotAndResetSequence()
        {
            var world = World(); var initial = Empty((ulong)uint.MaxValue + 50, 9, 7);
            var first = Frame(initial, EmptyTick());
            var second = Frame(Empty(first.Position.Tick, 1, 7), EmptyTick());
            int advanced = 0; world.OnBeltWorldTickApplied.Subscribe(_ => advanced++);
            world.ReceiveFrame(second); world.ReceiveFrame(first); world.ReceiveSnapshot(initial);
            world.ReceiveFrame(first); world.ReceiveFrame(second);
            Assert.AreEqual(second.Position.Tick, world.Position.Tick); Assert.AreEqual(1, world.Position.Sequence);
            Assert.AreEqual(2, advanced); Assert.AreEqual(BeltStreamStatus.Running, world.Status);
            world.Simulation.Dispose();
        }
        [Test]
        public void GapAndGenerationMismatchWaitForReplacementIncludingEmptyGraph()
        {
            var world = World(); world.ReceiveSnapshot(Single(0, 0, 1));
            var replacement = Empty(1, 2, 2); var frame = Frame(replacement, EmptyTick());
            world.ReceiveFrame(frame);
            Assert.AreEqual(BeltStreamStatus.Recovering, world.Status);
            world.ReceiveSnapshot(replacement);
            Assert.AreEqual(2UL, world.Position.Tick); Assert.IsEmpty(world.Routes);
            world.ReceiveSnapshot(Single(2, 2, 3));
            Assert.AreEqual(3UL, world.Generation); Assert.AreEqual(1, world.Routes.Length);
            world.ReceiveSnapshot(Empty(2, 3, 4)); world.ReceiveSnapshot(Single(2, 4, 5));
            Assert.AreEqual(5UL, world.Generation); Assert.AreEqual(4U, world.Position.Sequence);
            world.Simulation.Dispose();
        }
        [Test]
        public void HashMismatchNeedsRealLaterStateAndSuppressesAdvance()
        {
            var snapshot = Single(2, 1, 3); var frame = Frame(snapshot, EmptyTick()); var world = World();
            world.ReceiveSnapshot(snapshot); int advanced = 0; world.OnBeltWorldTickApplied.Subscribe(_ => advanced++);
            world.ReceiveFrame(new(frame.Previous, frame.Position, frame.Generation, frame.PreviousHash + 1, frame.Replay));
            Assert.AreEqual(BeltStreamStatus.Recovering, world.Status); Assert.AreEqual(0, advanced);
            var server = new BeltReplaySimulation(snapshot.Simulation); server.ApplyTick(frame.Replay, false);
            world.ReceiveSnapshot(new(frame.Position, 3, server.CaptureSnapshot(), snapshot.Routes));
            Assert.AreEqual(BeltStreamStatus.Running, world.Status);
            GpuBeltReplayReadback.AssertMatches(server.CaptureSnapshot(), world.Simulation, 3);
            world.Simulation.Dispose();
        }
        [Test]
        public void OverflowDropsBufferAndRecordsRecoveryReason()
        {
            var world = World(); var snapshot = Empty(0, 0, 1);
            string recoveryReason = null; world.OnRecoveryRequested.Subscribe(reason => recoveryReason = reason);
            for (ulong tick = 0; tick < 257; tick++) world.ReceiveFrame(Frame(Empty(tick, 1, 1), EmptyTick()));
            Assert.AreEqual(BeltStreamStatus.Recovering, world.Status); StringAssert.Contains("256", recoveryReason);
            world.ReceiveSnapshot(Empty(257, 1, 1)); Assert.AreEqual(BeltStreamStatus.Running, world.Status); world.Simulation.Dispose();
        }
        [Test]
        public void CpuThenGpuFailureIsNeverPublishedOrAutomaticallyRecovered()
        {
            var initial = Single(0, 0, 1); var world = World(); world.ReceiveSnapshot(initial);
            world.Simulation.Dispose(); int advanced = 0; world.OnBeltWorldTickApplied.Subscribe(_ => advanced++);
            LogAssert.Expect(LogType.Error, new Regex("\\[BeltWorld\\] Local state apply failed:"));
            Assert.Catch<Exception>(() => world.ReceiveFrame(Frame(initial, EmptyTick())));
            Assert.AreEqual(BeltStreamStatus.Failed, world.Status); Assert.AreEqual(0UL, world.Position.Tick); Assert.AreEqual(0, advanced);
            world.ReceiveSnapshot(initial); Assert.AreEqual(BeltStreamStatus.Failed, world.Status);
            world.Recover("late recovery"); Assert.AreEqual(BeltStreamStatus.Failed, world.Status);
        }
        [Test]
        public void InvalidExternalIdIsRejectedBeforeCpuMutation()
        {
            var initial = Single(0, 0, 1); var world = World(); world.ReceiveSnapshot(initial);
            var invalid = new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), new[] { 99 }, Array.Empty<int>(), Array.Empty<BeltReplayInsertion>());
            world.ReceiveFrame(Frame(initial, invalid));
            Assert.AreEqual(BeltStreamStatus.Recovering, world.Status);
            Assert.AreEqual(240, world.CaptureCpuState().Segments[0].Items[0].DistanceToExit); world.Simulation.Dispose();
        }
        [TestCase(false)][TestCase(true)]
        public void ExistingRunningOrBufferedIdentityIsRejectedBeforeCpuAndGpuMutation(bool buffered)
        {
            var initial = Single(0, 0, 1); var item = initial.Simulation.Segments[0].Items[0].Item;
            if (buffered)
                initial = new(initial.Position, 1, new(new[] { BeltReplaySegmentState.Merge(16, 0, Array.Empty<BeltItemState>(), item) },
                    Array.Empty<BeltReplayLink>(), new[] { new BeltReplayInput(0, BeltDirection.Back), new BeltReplayInput(0, BeltDirection.Left) }, Array.Empty<BeltReplayOutput>()), initial.Routes);
            var world = World(); world.ReceiveSnapshot(initial); int published = 0;
            world.OnBeltWorldTickApplied.Subscribe(_ => published++);
            var tick = new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), new[] { 0 }, Array.Empty<int>(), new[] { new BeltReplayInsertion(0, 16, item) });
            world.ReceiveFrame(Frame(initial, tick));
            Assert.AreEqual(BeltStreamStatus.Recovering, world.Status); Assert.AreEqual(0, published);
            Assert.AreEqual(new BeltReplaySimulation(initial.Simulation).ComputeStateHash(), new BeltReplaySimulation(world.CaptureCpuState()).ComputeStateHash());
            GpuBeltReplayReadback.AssertMatches(initial.Simulation, world.Simulation, 0); world.Simulation.Dispose();
        }
    }
}
