using System.IO;
using System.Reflection;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Starter.Playtest;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    // 同じプロセスでの再試行が、失敗した試行と同じセッション名へ印を書き直して証拠を消さないことを押さえる（ADR 0060 裁定5・ADR 0065）
    // Pins that a retry in the same process never rewrites the failed attempt's marks under the same session name and erases its evidence (ADR 0060 adjudication 5, ADR 0065)
    public class PreviousSessionStartupTasksTest
    {
        private const int TestProcessId = 424242;

        // セッション名はプロセス共有のstatic。このテストが進めたままにすると、先に印を置いた書き手の正常終了が別の名前の下へ書かれ旧セッションが異常終了として残る
        // The session name is a process-wide static; leaving it advanced would write an earlier writer's clean exit under a different name and leave its session looking like a crash
        private static readonly FieldInfo CurrentSessionNameField = typeof(ProcessSessionScope).GetField("_currentSessionName", BindingFlags.Static | BindingFlags.NonPublic);

        private string _sessionNameBeforeTest;

        [SetUp]
        public void SaveCurrentSessionName()
        {
            // getter を通すと未開始の状態まで開始してしまうので、フィールドを直接読んで null のまま保存する
            // Reading through the getter would start an unstarted session, so the field is read directly and a null is preserved as null
            _sessionNameBeforeTest = (string)CurrentSessionNameField.GetValue(null);
        }

        // 印は実ユーザーの last-session 配下に書かれる。残すと次回の起動が「前回異常終了」として退避・確認する（開発機を汚す）
        // The marks land under the real user's last-session directory; leaving them would make the next boot salvage and confirm a "previous crash" (it dirties the dev machine)
        [TearDown]
        public void DeleteTestProcessMarks()
        {
            CurrentSessionNameField.SetValue(null, _sessionNameBeforeTest);
            var marksRoot = Path.Combine(GameSystemPaths.BugReportLastSessionDirectory, CleanExitMarker.MarksDirectoryName);
            var processDirectory = RecordingProcessDirectories.DirectoryFor(marksRoot, TestProcessId);
            if (Directory.Exists(processDirectory)) Directory.Delete(processDirectory, true);
        }

        [Test]
        public void 再試行のたびに別のセッション名になり失敗した試行の印は正常終了へ畳まれない()
        {
            ProcessSessionScope.BeginNewSession();
            var failedAttempt = ProcessSessionScope.CurrentSessionName;
            ProcessSessionScope.BeginNewSession();
            var retriedAttempt = ProcessSessionScope.CurrentSessionName;
            Assert.AreNotEqual(failedAttempt, retriedAttempt, "再試行が失敗した試行と同じセッション名を使っている");

            // 失敗した試行は開始の印だけを残し、成功した再試行が正常終了の印を置く
            // The failed attempt leaves only its start mark while the successful retry places the clean-exit mark
            var origin = new SessionOriginSnapshot(null, BuildOriginReading.Editor(), null);
            CleanExitMarker.MarkSessionStarted(TestProcessId, failedAttempt, origin);
            CleanExitMarker.MarkSessionStarted(TestProcessId, retriedAttempt, origin);
            CleanExitMarker.MarkCleanExit(TestProcessId, retriedAttempt);

            Assert.IsFalse(CleanExitMarker.ConsumeSessionMarks(TestProcessId, failedAttempt).ExitedCleanly, "失敗した試行が正常終了として畳まれている");
            Assert.IsTrue(CleanExitMarker.ConsumeSessionMarks(TestProcessId, retriedAttempt).ExitedCleanly);
        }

        // 退避済みでもセッション名は更新する。退避の分岐の中へ移すと、再試行が失敗した試行の段へ書き戻る
        // The session name is renewed even when the salvage already ran; moving it into that branch would send a retry back into the failed attempt's level
        [Test]
        public void RunAtStartupは退避の判断より前にセッション名を更新する()
        {
            var runAtStartup = typeof(PreviousSessionStartupTasks).GetMethod(nameof(PreviousSessionStartupTasks.RunAtStartup), BindingFlags.Static | BindingFlags.Public);
            var beginNewSession = typeof(ProcessSessionScope).GetMethod(nameof(ProcessSessionScope.BeginNewSession), BindingFlags.Static | BindingFlags.Public);
            var unattendedReason = typeof(PlaytestStartGateBypass).GetMethod(nameof(PlaytestStartGateBypass.UnattendedReason), BindingFlags.Static | BindingFlags.Public);

            Assert.IsTrue(MethodCallInspector.CallsInOrder(runAtStartup, beginNewSession, unattendedReason));
        }
    }
}
