using System.Collections.Generic;
using System.Reflection;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Starter.Playtest;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Starter
{
    public class PreviousSessionStartupTasksTest
    {
        private readonly List<string> _sessions = new();

        [SetUp]
        public void SetUp()
        {
            GameShutdownEvent.ResetForNewSession();
        }

        [TearDown]
        public void TearDown()
        {
            // 共有の購読を残すと、別テストの終了通知が消費済みの印を再生成する
            // A retained subscription would recreate consumed marks on another test's shutdown
            var subscriptions = typeof(CleanExitMarkWriter).GetField("_subscriptions", BindingFlags.NonPublic | BindingFlags.Static);
            ((CompositeDisposable)subscriptions.GetValue(null))?.Dispose();
            subscriptions.SetValue(null, null);
            foreach (var session in _sessions)
                CleanExitMarker.ConsumeSessionMarks(RecordingProcessDirectories.CurrentProcessId(), session);
            _sessions.Clear();
            GameShutdownEvent.ResetForNewSession();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void BeginCurrentSessionMarks_WritesCleanBeforeHostOrConsent(bool isRemoteConnection, bool hostReady)
        {
            PreviousSessionStartupTasks.BeginCurrentSessionMarks();
            var session = ProcessSessionScope.CurrentSessionName;
            _sessions.Add(session);
            var processId = RecordingProcessDirectories.CurrentProcessId();

            // 終了印は記録収集の可否が確定する前に存在する
            // Exit marks exist before record-collection eligibility is known
            Assert.IsTrue(ContainsSession(processId, session));
            Assert.AreEqual(!isRemoteConnection && hostReady, PlaytestRecordCollection.Decide(isRemoteConnection, hostReady));
            Assert.IsTrue(GameShutdownEvent.NotifyUnannouncedExit());
            var record = CleanExitMarker.ConsumeSessionMarks(processId, session);
            Assert.IsTrue(record.ExitedCleanly);
            Assert.IsFalse(record.ShutdownStalled);
            Assert.IsNotNull(record.Origin);
        }

        [Test]
        public void BeginCurrentSessionMarks_ReplayPreservesCurrentMarksDuringPreviousSessionSelection()
        {
            PreviousSessionStartupTasks.BeginCurrentSessionMarks();
            var previous = ProcessSessionScope.CurrentSessionName;
            _sessions.Add(previous);
            GameShutdownEvent.NotifyUnannouncedExit();

            GameShutdownEvent.ResetForNewSession();
            PreviousSessionStartupTasks.BeginCurrentSessionMarks();
            var current = ProcessSessionScope.CurrentSessionName;
            _sessions.Add(current);
            var processId = RecordingProcessDirectories.CurrentProcessId();

            // 退避が現在の印を前回として消費すると、初期化途中の停止を記録できなくなる
            // Consuming current marks as previous would lose protection during initialization
            var scan = PreviousProcessScanner.Scan(processId, current, new RecordingProcessTakeover(), CleanExitMarker.MarkedSessions(), new[] { processId });
            Assert.AreNotEqual(previous, current);
            Assert.IsTrue(scan.Sessions.Exists(session => session.ProcessId == processId && session.SessionName == previous));
            Assert.IsFalse(scan.Sessions.Exists(session => session.ProcessId == processId && session.SessionName == current));
            Assert.IsTrue(ContainsSession(processId, current));
            Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(processId, previous).ExitedCleanly);

            GameShutdownEvent.NotifyUnannouncedExit();
            Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(processId, current).ExitedCleanly);
        }

        private static bool ContainsSession(int processId, string sessionName)
        {
            foreach (var session in CleanExitMarker.MarkedSessions())
                if (session.ProcessId == processId && session.SessionName == sessionName) return true;
            return false;
        }
    }
}
