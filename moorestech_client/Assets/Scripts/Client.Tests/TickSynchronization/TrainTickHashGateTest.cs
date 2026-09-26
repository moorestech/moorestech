using UniRx;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.TickSynchronization
{
    public class TrainTickHashGateTest
    {
        private TrainTickContext _context;
        private RailGraphClientCache _rails;
        private TrainUnitClientCache _trains;
        private TrainFullSnapshotEventNetworkHandler _handler;
        private TrainUnitHashVerifier _gate;

        [SetUp]
        public void SetUp()
        {
            _context = new TrainTickContext();
            _rails = (RailGraphClientCache)Activator.CreateInstance(typeof(RailGraphClientCache), true);
            _trains = new TrainUnitClientCache(_rails);
            _handler = new TrainFullSnapshotEventNetworkHandler(null, null, _context);
            _gate = new TrainUnitHashVerifier(_context, _trains, _rails);
        }

        [TearDown]
        public void TearDown()
        {
            _handler.Dispose();
            Client.Game.Common.GameShutdownEvent.ResetForNewSession();
        }

        [Test]
        public void DummyHash_AllowsAdvanceAndRecordsAppliedId()
        {
            _context.Hashes.EnqueueHash(TrainUnitHashBuffer.DummyHash, TrainUnitHashBuffer.DummyHash, 0, 1);
            Assert.IsTrue(_gate.CanAdvanceTick(1));
            Assert.AreEqual(1ul, _context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void MatchingHash_AllowsAdvanceAndRecordsAppliedId()
        {
            _context.Hashes.EnqueueHash(_trains.ComputeCurrentHash(), _rails.ComputeCurrentHash(), 0, 1);
            Assert.IsTrue(_gate.CanAdvanceTick(1));
            Assert.AreEqual(1ul, _context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void FutureHashOnly_ForceSlipsWithoutRecordingAppliedId()
        {
            _context.Hashes.EnqueueHash(0, 0, 1, 1);
            LogAssert.Expect(LogType.Warning, "tick force slip! expected=0_1, firstBuffered=1_1");
            Assert.IsTrue(_gate.CanAdvanceTick(1));
            Assert.AreEqual(0ul, _context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void NoHash_WaitsWithoutWarning()
        {
            Assert.IsFalse(_gate.CanAdvanceTick(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void HashReads_DoNotConsumeAndDiscardIsStrictlyOlder()
        {
            _context.Hashes.EnqueueHash(12, 34, 0, 1);
            Assert.IsTrue(_context.Hashes.TryDequeueHashAtTickSequenceId(1, out var first));
            Assert.IsTrue(_context.Hashes.TryDequeueHashAtTickSequenceId(1, out var second));
            Assert.AreEqual(first, second);
            _context.Hashes.DiscardHashesOlderThan(1);
            Assert.IsTrue(_context.Hashes.TryGetFirstHashTickUnifiedId(out var id));
            Assert.AreEqual(1ul, id);
            _context.Hashes.DiscardHashesOlderThan(2);
            Assert.IsFalse(_context.Hashes.TryGetFirstHashTickUnifiedId(out _));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MismatchingHash_StopsSubsequentFramesAndQuitsOnceWithoutSaving(bool trainMismatch)
        {
            Client.Game.Common.GameShutdownEvent.ResetForNewSession();
            var fatalCount = 0;
            using var subscription = Client.Game.Common.GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
            {
                Assert.AreEqual(Client.Game.Common.GameShutdownReason.FatalSynchronizationFailure, reason);
                fatalCount++;
            });
            var expectedTrain = _trains.ComputeCurrentHash() ^ (trainMismatch ? 1u : 0u);
            var expectedRail = _rails.ComputeCurrentHash() ^ (trainMismatch ? 0u : 1u);
            _context.Hashes.EnqueueHash(expectedTrain, expectedRail, 0, 1);
            var pending = new CountingEvent();
            _context.Events.EnqueueEvent(0, 2, pending);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TrainUnitHashVerifier\\] Hash mismatch detected"));
            // EditModeのframe時間に依存せず、そのframeのsimulator内でhashを照合する。
            // Verify the hash inside the simulator frame independently of EditMode frame time.
            _context.CompleteInitialSnapshot();
            Client.Tests.Common.TestReflection.SetField(_context.AdvanceController, "_estimatedClientTick", 2d);
            new TrainUnitClientSimulator(_context, _gate, null).Tick();

            // 次frameのflushとvisualも停止する。nullのvisual依存へ到達したら失敗する。
            // Stop the next frame's flush and visual update; reaching the null visual dependency fails.
            _context.AdvanceController.Advance(0.1f, _gate);
            new TrainUnitClientSimulator(_context, _gate, null).Tick();
            Assert.IsFalse(_context.Events.TryFlushEvent(2));
            Assert.IsFalse(_gate.CanAdvanceTick(1));
            Assert.AreEqual(0, pending.Applied);
            Assert.AreEqual(0ul, _context.State.GetAppliedTickUnifiedId());
            Assert.AreEqual(1, fatalCount);
            Client.Game.Common.GameShutdownEvent.ResetForNewSession();
        }

        private sealed class CountingEvent : Client.Game.InGame.Train.Network.ITrainTickBufferedEvent
        {
            public int Applied;
            public void Apply() => Applied++;
        }
    }
}
