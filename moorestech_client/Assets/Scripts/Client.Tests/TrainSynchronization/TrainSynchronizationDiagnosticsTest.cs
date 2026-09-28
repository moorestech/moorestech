using System;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.Unit;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.TrainSynchronization
{
    public sealed class TrainSynchronizationDiagnosticsTest
    {
        private TrainSynchronizationTestContext _context;

        [SetUp]
        public void SetUp() => _context = new TrainSynchronizationTestContext();

        [TearDown]
        public void TearDown() => _context.Dispose();

        [Test]
        public void ConfirmedGap_SavesWithFrozenOnsetAndBoundedRecentHistory()
        {
            _context.Initialize(10);
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            _context.Diagnostics.RecordReceived("before", 10, 0);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            Assert.That(_context.Reports(), Is.Empty);

            // 上書き・古い到着で履歴が循環しても初回の証拠は保持する。
            // Keep onset evidence even after stale and duplicate arrivals wrap recent history.
            for (var i = 0; i < 300; i++) _context.Diagnostics.RecordReceived("stale", 9, (uint)i);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 2);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            var report = _context.ReadReport();
            Assert.That((string)report["CaptureReason"], Is.EqualTo("ConfirmedOrderedGap"));
            Assert.That((uint)report["TickGapAtOnset"], Is.Zero);
            Assert.That((uint)report["TickGapAtCapture"], Is.Zero);
            Assert.That((ulong)report["ExpectedId"], Is.EqualTo(expected));
            Assert.That((uint)report["ExpectedTick"], Is.EqualTo(10));
            Assert.That((uint)report["ExpectedSequenceId"], Is.EqualTo(1));
            Assert.That((ulong)report["AppliedIdAtOnset"], Is.EqualTo(expected - 1));
            Assert.That(report["HashComparison"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)report["OnsetHistory"][0]["Kind"], Is.EqualTo("before"));
            Assert.That((JArray)report["OnsetHistory"], Has.Count.EqualTo(1));
            Assert.That((JArray)report["RecentHistory"], Has.Count.EqualTo(256));
            Assert.That((DateTime)report["CapturedAtUtc"], Is.GreaterThanOrEqualTo((DateTime)report["WaitingSinceUtc"]));
        }

        [Test]
        public void MissingMessageSaving_RequiresInitialSuccessAndLaterArrival()
        {
            Assert.That(_context.Gate.CanAdvanceTick(1), Is.False);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 200, 1);
            Assert.That(_context.Gate.CanAdvanceTick(1), Is.False);
            Assert.That(_context.Reports(), Is.Empty);
            _context.Initialize(201);
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            for (var i = 0; i < 10000; i++) Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            Assert.That(_context.Reports(), Is.Empty);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 201, 2);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            var report = _context.ReadReport();
            Assert.That((string)report["CaptureReason"], Is.EqualTo("ConfirmedOrderedGap"));
            Assert.That((uint)report["TickGapAtCapture"], Is.Zero);
        }

        [Test]
        public void ShortWaitCanResume_ConfirmedWaitWritesOnlyOnce()
        {
            _context.Initialize(10);
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.False);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 1);
            Assert.That(_context.Gate.CanAdvanceTick(expected), Is.True);
            Assert.That(_context.Reports(), Is.Empty);

            // 通常待ちは再開し、確定後は後着でも同じ停止を維持する。
            // Resume ordinary waits, but retain the same terminal stop even after late arrivals.
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 3);
            for (var i = 0; i < 1000; i++) Assert.That(_context.Gate.CanAdvanceTick(expected + 1), Is.False);
            _context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 10, 2);
            Assert.That(_context.Gate.CanAdvanceTick(expected + 1), Is.False);
            Assert.That(_context.State.IsPermanentlyWaiting, Is.True);
            Assert.That(_context.Reports(), Has.Length.EqualTo(1));
            Assert.That(_context.Warnings, Has.Count.EqualTo(1));
        }

        [Test]
        public void BufferArrivalHistory_IncludesStaleMessagesAndDuplicateOverwrites()
        {
            _context.Initialize(10);
            _context.Buffer.EnqueueHash(1, 2, 9, 7);
            _context.Buffer.EnqueueHash(3, 4, 10, 0);
            _context.Buffer.EnqueueHash(5, 6, 10, 3);
            _context.Buffer.EnqueueHash(7, 8, 10, 3);
            Assert.That(_context.Gate.CanAdvanceTick(_context.State.GetAppliedTickUnifiedId() + 1), Is.False);
            _context.Buffer.EnqueueHash(9, 10, 210, 1);
            Assert.That(_context.Gate.CanAdvanceTick(_context.State.GetAppliedTickUnifiedId() + 1), Is.False);
            var history = (JArray)_context.ReadReport()["OnsetHistory"];
            Assert.That(history, Has.Count.EqualTo(4));
            Assert.That((uint)history[0]["Tick"], Is.EqualTo(9));
            Assert.That((uint)history[2]["SequenceId"], Is.EqualTo(3));
            Assert.That((uint)history[3]["SequenceId"], Is.EqualTo(3));
        }

        [Test]
        public void DiskFailure_IsReportedAndNotRetriedEveryFrame()
        {
            Directory.CreateDirectory(_context.DirectoryPath);
            var blockedPath = Path.Combine(_context.DirectoryPath, "file-instead-of-directory");
            File.WriteAllText(blockedPath, "occupied");
            var diagnostics = new TrainSynchronizationDiagnostics(_context.State, new TrainSynchronizationDiagnosticWriter(blockedPath));
            var buffer = new TrainUnitFutureMessageBuffer(_context.State, diagnostics);
            var gate = new Client.Game.InGame.Train.View.TrainUnitHashVerifier(buffer, _context.Trains, _context.Rail, _context.State, diagnostics);
            _context.Initialize(0);
            buffer.EnqueueHash(_context.Trains.ComputeCurrentHash() ^ 1, _context.Rail.ComputeCurrentHash(), 0, 1);
            Assert.That(gate.CanAdvanceTick(1), Is.False);
            buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, 200, 1);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TrainSynchronization\\] Diagnostic save failed:"));
            Assert.DoesNotThrow(() => gate.CanAdvanceTick(1));
            Assert.That(_context.State.IsPermanentlyWaiting, Is.True);
            Assert.That(diagnostics.LastWriteResult.FailureReason, Is.Not.Empty);
            var result = diagnostics.LastWriteResult;
            for (var i = 0; i < 1000; i++) Assert.That(gate.CanAdvanceTick(1), Is.False);
            Assert.That(diagnostics.LastWriteResult, Is.SameAs(result));
            Assert.That(File.ReadAllText(blockedPath), Is.EqualTo("occupied"));
        }
    }
}
