using System;
using System.IO;
using System.Text.RegularExpressions;
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
        public void ReceivedTickGap_SavesAt200WithFrozenOnsetAndBoundedRecentHistory()
        {
            _context.InitializeDiagnostics(10);
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            _context.Diagnostics.RecordReceived("before", 10, 0);
            _context.Diagnostics.RecordMissingOrderedMessage(expected);
            _context.Diagnostics.RecordReceived("Hash", 209, 1);
            _context.Diagnostics.RecordMissingOrderedMessage(expected);
            Assert.That(_context.Reports(), Is.Empty);

            // 上書き・古い到着で履歴が循環しても初回の証拠は保持する。
            // Keep onset evidence even after stale and duplicate arrivals wrap recent history.
            for (var i = 0; i < 300; i++) _context.Diagnostics.RecordReceived("stale", 9, (uint)i);
            _context.Diagnostics.RecordReceived("Hash", 210, 2);
            _context.Diagnostics.RecordMissingOrderedMessage(expected);
            var report = _context.ReadReport();
            Assert.That((string)report["CaptureReason"], Is.EqualTo("ReceivedTickGap"));
            Assert.That((uint)report["TickGapAtOnset"], Is.Zero);
            Assert.That((uint)report["TickGapAtCapture"], Is.EqualTo(200));
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
        public void Saving_RequiresInitialSuccessAndObservedFutureTickGap()
        {
            _context.Diagnostics.RecordMissingOrderedMessage(1);
            _context.Diagnostics.RecordReceived("Hash", 200, 1);
            Assert.That(_context.Reports(), Is.Empty);
            _context.InitializeDiagnostics(200);
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            for (var i = 0; i < 10000; i++) _context.Diagnostics.RecordMissingOrderedMessage(expected);
            Assert.That(_context.Reports(), Is.Empty);
            _context.Diagnostics.RecordReceived("Hash", 400, 1);
            _context.Diagnostics.RecordMissingOrderedMessage(expected);
            var report = _context.ReadReport();
            Assert.That((string)report["CaptureReason"], Is.EqualTo("ReceivedTickGap"));
            Assert.That((uint)report["TickGapAtCapture"], Is.EqualTo(200));
        }

        [Test]
        public void ShortWaitAndRepeatedEvaluation_DoNotWriteAgainUntilRecovery()
        {
            _context.InitializeDiagnostics(10);
            var expected = _context.State.GetAppliedTickUnifiedId() + 1;
            _context.Diagnostics.RecordMissingOrderedMessage(expected);
            _context.Diagnostics.RecordApplied(expected);
            _context.Diagnostics.RecordReceived("Hash", 11, 0);
            Assert.That(_context.Reports(), Is.Empty);

            // 一度保存した停止は抑制し、適用通知後の別停止は新しい診断にする。
            // Suppress repeats for one stall and create a new diagnostic after applied recovery.
            _context.Diagnostics.RecordMissingOrderedMessage(expected);
            _context.Diagnostics.RecordReceived("Hash", 210, 0);
            for (var i = 0; i < 1000; i++) _context.Diagnostics.RecordMissingOrderedMessage(expected);
            Assert.That(_context.Reports(), Has.Length.EqualTo(1));
            Assert.That(_context.Warnings, Has.Count.EqualTo(1));
            _context.State.RecordAppliedTickUnifiedId(expected);
            _context.Diagnostics.RecordApplied(expected);
            _context.Diagnostics.RecordMissingOrderedMessage(expected + 1);
            Assert.That(_context.Reports(), Has.Length.EqualTo(2));
            Assert.That(_context.Warnings, Has.Count.EqualTo(2));
        }

        [Test]
        public void BufferArrivalHistory_IncludesStaleMessagesAndDuplicateOverwrites()
        {
            _context.InitializeDiagnostics(10);
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
            diagnostics.Initialize(0);
            diagnostics.RecordMissingOrderedMessage(1);
            LogAssert.Expect(LogType.Error, new Regex("^\\[TrainSynchronization\\] Diagnostic save failed:"));
            diagnostics.RecordReceived("Hash", 200, 0);
            Assert.DoesNotThrow(() => diagnostics.RecordMissingOrderedMessage(1));
            Assert.That(diagnostics.LastWriteResult.FailureReason, Is.Not.Empty);
            var result = diagnostics.LastWriteResult;
            for (var i = 0; i < 1000; i++) diagnostics.RecordMissingOrderedMessage(1);
            Assert.That(diagnostics.LastWriteResult, Is.SameAs(result));
            Assert.That(File.ReadAllText(blockedPath), Is.EqualTo("occupied"));
        }
    }
}
