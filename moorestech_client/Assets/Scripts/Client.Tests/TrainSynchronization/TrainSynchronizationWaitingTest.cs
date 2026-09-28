using System.Text.RegularExpressions;
using Client.Game.InGame.Train.Network;
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
            _context.InitializeDiagnostics(10);
        }

        [TearDown]
        public void TearDown() => _context.Dispose();

        [TestCase("FutureHash")]
        [TestCase("SameTickGap")]
        [TestCase("FutureEvent")]
        [TestCase("Empty")]
        public void MissingId_DoesNotAdvanceAfter1000Attempts_AndLateHashResumes(string scenario)
        {
            var before = _context.State.GetAppliedTickUnifiedId();
            var laterEvent = new CountingEvent();
            if (scenario == "FutureHash") _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 11, 1);
            if (scenario == "SameTickGap") _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 2);
            if (scenario == "FutureEvent") _context.Buffer.EnqueueEvent(11, 1, laterEvent);
            for (var i = 0; i < 1000; i++) Assert.That(_context.Gate.CanAdvanceTick(before + 1), Is.False);
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(before));
            Assert.That(laterEvent.AppliedCount, Is.Zero);
            Assert.That(_context.Reports(), Is.Empty);
            Assert.That(_context.Warnings.Count, Is.LessThanOrEqualTo(1));

            // 必要な順序位置の後着だけが待機を解消する。
            // Only arrival at the required ordered position resolves the wait.
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 1);
            Assert.That(_context.Gate.CanAdvanceTick(before + 1), Is.True);
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(before + 1));
            Assert.That(_context.Diagnostics.IsWaiting, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MatchingAndDummyHashes_AdvanceNormally(bool dummy)
        {
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            var trainHash = dummy ? uint.MaxValue : _context.Trains.ComputeCurrentHash();
            var railHash = dummy ? uint.MaxValue : _context.Rail.ComputeCurrentHash();
            _context.Buffer.EnqueueHash(trainHash, railHash, 10, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.True);
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(expected));
            Assert.That(_context.Reports(), Is.Empty);
            Assert.That(_context.Warnings, Is.Empty);
        }

        [Test]
        public void HashMismatch_RemainsAtSameId_AndRecordsHashesBeforeCorrection()
        {
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            var localTrain = _context.Trains.ComputeCurrentHash();
            var localRail = _context.Rail.ComputeCurrentHash();
            _context.Buffer.EnqueueHash(localTrain ^ 1, localRail ^ 1, 10, 1);
            for (var i = 0; i < 1000; i++) Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            Assert.That(_context.State.GetAppliedTickUnifiedId(), Is.EqualTo(expected - 1));
            Assert.That(_context.Warnings, Has.Count.EqualTo(1));
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 210, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            var report = _context.ReadReport();
            Assert.That((string)report["WaitingReason"], Is.EqualTo("HashMismatch"));
            Assert.That((uint)report["HashComparison"]["LocalTrainHash"], Is.EqualTo(localTrain));
            Assert.That((uint)report["HashComparison"]["ServerTrainHash"], Is.EqualTo(localTrain ^ 1));
            Assert.That((uint)report["HashComparison"]["LocalRailHash"], Is.EqualTo(localRail));
            Assert.That((uint)report["HashComparison"]["ServerRailHash"], Is.EqualTo(localRail ^ 1));
            _context.Buffer.EnqueueHash(localTrain, localRail, 10, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.True);
            Assert.That(_context.Diagnostics.IsWaiting, Is.False);
        }

        [Test]
        public void Simulator_LateEventAppliesBeforeHash_AndDoesNotSkipFutureEvent()
        {
            var missing = new CountingEvent();
            var future = new CountingEvent();
            _context.Buffer.EnqueueEvent(10, 2, future);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 3);
            for (var i = 0; i < 1000; i++) _context.Simulator.Tick();
            Assert.That(future.AppliedCount, Is.Zero);
            Assert.That(_context.State.GetTickSequenceId(), Is.Zero);
            _context.Buffer.EnqueueEvent(10, 1, missing);
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
            Assert.That(_context.Diagnostics.IsWaiting, Is.True);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 1);
            for (uint tick = 11; tick <= 220; tick++)
            {
                _context.Buffer.EnqueueEvent(tick, 1, new CountingEvent());
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
            Assert.That(fresh.Diagnostics.IsWaiting, Is.False);
            Assert.That(fresh.Reports(), Is.Empty);
        }

        private sealed class CountingEvent : ITrainTickBufferedEvent
        {
            internal int AppliedCount;
            public void Apply() => AppliedCount++;
        }
    }
}
