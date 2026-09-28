using System.Collections.Generic;
using System.Reflection;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.RemoteExec;
using Client.RemoteExec.Access;
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

        // 収集同意（リモート接続なら集めない）と独立に終了印を書く。WebUiHostの起動有無は条件にしない（ADR 0065）
        // Exit marks are written regardless of collection consent (a remote connection collects nothing); whether WebUiHost started is no condition (ADR 0065)
        [TestCase(false)]
        [TestCase(true)]
        public void BeginCurrentSessionMarks_WritesCleanBeforeHostOrConsent(bool isRemoteConnection)
        {
            PreviousSessionStartupTasks.BeginCurrentSessionMarks();
            var session = ProcessSessionScope.CurrentSessionName;
            _sessions.Add(session);
            var processId = RecordingProcessDirectories.CurrentProcessId();

            Assert.IsTrue(ContainsSession(processId, session));
            Assert.AreEqual(!isRemoteConnection, PlaytestRecordCollection.Decide(isRemoteConnection));
            Assert.IsTrue(GameShutdownEvent.NotifyUnannouncedExit());
            var record = CleanExitMarker.ConsumeSessionMarks(processId, session);
            Assert.IsTrue(record.ExitedCleanly);
            Assert.IsFalse(record.ShutdownStalled);
            Assert.IsNotNull(record.Origin);
        }

        // 箱へ印を載せる配線を守る。起動フラグと書き込まれた開始印の対応が壊れても他のテストは緑のままなので専用に固定する
        // Pins the wiring from the launch flag to the written start mark; the other tests stay green even if this correspondence breaks, so it needs its own case
        [Test]
        public void BeginCurrentSessionMarks_起動フラグが有効な開始印の遠隔実行印を書く()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { RemoteExecLaunchOption.Marker });
            try
            {
                PreviousSessionStartupTasks.BeginCurrentSessionMarks();
                var session = ProcessSessionScope.CurrentSessionName;
                _sessions.Add(session);
                var processId = RecordingProcessDirectories.CurrentProcessId();

                var record = CleanExitMarker.ConsumeSessionMarks(processId, session);
                Assert.IsNotNull(record.Origin, record.OriginMissingReason);
                Assert.IsNotNull(record.Origin.RemoteExec, "起動フラグ有効なのに開始印へ遠隔実行の印が無い");
                Assert.AreEqual(RemoteExecLedger.CurrentFileName, record.Origin.RemoteExec.LedgerFileName);
            }
            finally
            {
                RemoteExecLaunchOption.ResolveFromCommandLine(new string[0]);
            }
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
