using System.Text.RegularExpressions;
using System.Reflection;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.Unit;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.TrainSynchronization
{
    public sealed class TrainSynchronizationWaitingTest
    {
        private TrainSynchronizationTestContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = new TrainSynchronizationTestContext();
            _context.Initialize(10);
        }

        [TearDown]
        public void TearDown() => _context.Dispose();

        [TestCase("FutureHash")]
        [TestCase("SameTickGap")]
        [TestCase("FutureEvent")]
        [TestCase("Empty")]
        public void MissingId_WaitsNormallyOrStopsPermanentlyWhenConfirmed(string scenario)
        {
            var before = _context.State.GetAppliedTickUnifiedId();
            var laterEvent = new CountingEvent();
            if (scenario == "FutureHash") _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 11, 1);
            if (scenario == "SameTickGap") _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 2);
            if (scenario == "FutureEvent") _context.Buffer.EnqueueEvent("test:event", 11, 1, laterEvent);
            Assert.That(_context.Gate.CanAdvanceTick(before + 1), Is.False);
            Assert.That(_context.Reports().Length, Is.EqualTo(scenario == "Empty" ? 0 : 1));
            for (var i = 0; i < 1000; i++) Assert.That(_context.Gate.CanAdvanceTick(before + 1), Is.False);
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(before));
            Assert.That(laterEvent.AppliedCount, Is.Zero);
            Assert.That(_context.Reports().Length, Is.EqualTo(scenario == "Empty" ? 0 : 1));
            Assert.That(_context.Warnings.Count, Is.LessThanOrEqualTo(1));

            // 後続のない通常待ちだけを後着で解消し、確定停止後は保持量を増やさない。
            // Resolve only ordinary waits on late arrival and keep retention bounded after a terminal stop.
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 1);
            var canResume = scenario == "Empty";
            Assert.That(_context.Gate.CanAdvanceTick(before + 1), Is.EqualTo(canResume));
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(canResume ? before + 1 : before));
            Assert.That(_context.State.IsWaiting, Is.False);
            if (canResume) return;
            for (uint tick = 20; tick < 10020; tick++)
            {
                _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, tick, 1);
                _context.Buffer.EnqueueEvent("test:discarded", tick, 2, laterEvent);
            }
            foreach (var fieldName in new[] { "_futureEvents", "_futureHashStates" })
            {
                var payloads = typeof(TrainUnitFutureMessageBuffer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_context.Buffer);
                Assert.That(payloads.GetType().GetProperty("Count").GetValue(payloads), Is.EqualTo(0));
            }
            var history = typeof(TrainSynchronizationDiagnostics).GetField("_history", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_context.Diagnostics);
            Assert.That(history.GetType().GetProperty("Count").GetValue(history), Is.EqualTo(256));
            Assert.That(_context.Reports(), Has.Length.EqualTo(1));
            Assert.That(laterEvent.AppliedCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MatchingAndDummyHashes_AdvanceNormally(bool dummy)
        {
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            var trainHash = dummy ? uint.MaxValue : _context.Trains.ComputeCurrentHash();
            var railHash = dummy ? uint.MaxValue : _context.Rail.ComputeCurrentHash();
            if (!dummy)
            {
                _context.Buffer.EnqueueHash(trainHash ^ 1, railHash, 10, 1);
                Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            }
            _context.Buffer.EnqueueHash(trainHash, railHash, 10, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.True);
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(expected));
            Assert.That(_context.Reports(), Is.Empty);
            Assert.That(_context.Warnings.Count, Is.EqualTo(dummy ? 0 : 1));
        }

        [Test]
        public void HashMismatch_RemainsAtSameId_AndStopsAt200TickGap()
        {
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            var localTrain = _context.Trains.ComputeCurrentHash();
            var localRail = _context.Rail.ComputeCurrentHash();
            _context.Buffer.EnqueueHash(localTrain ^ 1, localRail ^ 1, 10, 1);
            for (var i = 0; i < 1000; i++) Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(expected - 1));
            Assert.That(_context.Warnings, Has.Count.EqualTo(1));
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 209, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            Assert.That(_context.Reports(), Is.Empty);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 210, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            var report = _context.ReadReport();
            Assert.That((string)report["WaitingReason"], Is.EqualTo("HashMismatch"));
            Assert.That((uint)report["HashComparison"]["LocalTrainHash"], Is.EqualTo(localTrain));
            Assert.That((uint)report["HashComparison"]["ServerTrainHash"], Is.EqualTo(localTrain ^ 1));
            Assert.That((uint)report["HashComparison"]["LocalRailHash"], Is.EqualTo(localRail));
            Assert.That((uint)report["HashComparison"]["ServerRailHash"], Is.EqualTo(localRail ^ 1));
            _context.Buffer.EnqueueHash(localTrain, localRail, 10, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            Assert.That(_context.State.IsPermanentlyWaiting, Is.True);
        }

        [Test]
        public void MissingThenMismatchingHash_UsesCurrentObservationAndRetainsOnset()
        {
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            _context.Buffer.EnqueueHash(_context.Trains.ComputeCurrentHash() ^ 1, _context.Rail.ComputeCurrentHash(), 10, 1);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 209, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            Assert.That(_context.Reports(), Is.Empty);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 210, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            var report = _context.ReadReport();
            Assert.That((string)report["WaitingReason"], Is.EqualTo("MissingOrderedMessage"));
            Assert.That((string)report["WaitingReasonAtCapture"], Is.EqualTo("HashMismatch"));
            Assert.That((string)report["CaptureReason"], Is.EqualTo("ReceivedTickGap"));
            Assert.That((uint)report["TickGapAtOnset"], Is.Zero);
            Assert.That((uint)report["TickGapAtCapture"], Is.EqualTo(200));
        }

        [Test]
        public void Simulator_OrdinaryWaitResumesOnCompleteLateBundle()
        {
            var missing = new CountingEvent();
            var future = new CountingEvent();
            for (var i = 0; i < 1000; i++) _context.Simulator.Tick();
            Assert.That(future.AppliedCount, Is.Zero);
            Assert.That(_context.State.GetTickSequenceId(), Is.Zero);
            _context.Buffer.EnqueueEvent("test:event", 10, 1, missing);
            _context.Buffer.EnqueueEvent("test:event", 10, 2, future);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 3);
            _context.Simulator.Tick();
            Assert.That(missing.AppliedCount, Is.EqualTo(1));
            Assert.That(future.AppliedCount, Is.EqualTo(1));
            Assert.That(_context.State.GetTick(), Is.EqualTo(11));
        }

        [Test]
        public void CompleteBufferedBundles_ResolvePreviousWaitWithoutWarningsOrDiagnostic()
        {
            // 到着待ちの直後に大量受信しても、適用前の受信だけでは欠落と決めない。
            // A burst after an arrival wait must not be diagnosed before ordered application catches up.
            _context.Simulator.Tick();
            Assert.That(_context.State.IsWaiting, Is.True);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 1);
            for (uint tick = 11; tick <= 220; tick++)
            {
                _context.Buffer.EnqueueEvent("test:event", tick, 1, new CountingEvent());
                _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, tick, 2);
            }
            Assert.That(_context.Warnings, Is.Empty);
            Assert.That(_context.Reports(), Is.Empty);
            _context.Simulator.Tick();
            Assert.That(_context.State.GetTick(), Is.GreaterThan(10));
            Assert.That(_context.Warnings, Is.Empty);
            Assert.That(_context.Reports(), Is.Empty);
        }

        [Test]
        public void FailedRailSnapshot_DoesNotActivateDiagnosticsAfterTrainSnapshot()
        {
            using var fresh = new TrainSynchronizationTestContext();
            var handler = fresh.CreateSnapshotHandler();
            LogAssert.Expect(LogType.Error, new Regex("^\\[TrainFullSnapshot\\]"));
            TrainSynchronizationTestContext.Deliver(handler, "HandleRailGraphFullSnapshot", new byte[] { 0xC1 });
            fresh.ApplyTrainSnapshot(handler, 10);
            Assert.That(handler.WaitForInitialApplyAsync().Status, Is.EqualTo(UniTaskStatus.Faulted));
            Assert.Throws<MessagePackSerializationException>(() => handler.WaitForInitialApplyAsync().GetAwaiter().GetResult());
            fresh.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 210, 1);
            Assert.That(fresh.Gate.CanAdvanceTick(fresh.State.GetAppliedTickUnifiedId() + 1), Is.False);
            Assert.That(fresh.State.IsWaiting, Is.False);
            Assert.That(fresh.Reports(), Is.Empty);
        }

        private sealed class CountingEvent : ITrainTickBufferedEvent
        {
            internal int AppliedCount;
            public void Apply() => AppliedCount++;
        }
    }
}
