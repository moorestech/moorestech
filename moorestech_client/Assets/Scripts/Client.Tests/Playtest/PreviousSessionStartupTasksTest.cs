using System.Reflection;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Starter.Playtest;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    // 同じプロセスでの再試行が、失敗した試行と同じセッション名へ印を書き直して証拠を消さないことを押さえる（ADR 0060 裁定5・ADR 0065）
    // Pins that a retry in the same process never rewrites the failed attempt's marks under the same session name and erases its evidence (ADR 0060 adjudication 5, ADR 0065)
    public class PreviousSessionStartupTasksTest
    {
        private const int TestProcessId = 424242;

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
            var origin = new SessionOriginSnapshot(null, BuildOriginReading.Editor());
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
