using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 未応答のクラッシュ資料が、ゲートを出さない起動を跨いでも消えないこと（F04）と、退避結果の形（F12・F13・F19）を押さえる
    // Pins that unanswered crash evidence survives launches that show no gate (F04), and the shape of the salvage result (F12, F13, F19)
    public class PreviousSessionPendingTest
    {
        private const int DeadProcessId = 1234;
        private const int OtherDeadProcessId = 777;

        private string _root;
        private string _snapshots;
        private string _lastSession;

        [SetUp]
        public void CreateDirectories()
        {
            _root = Path.Combine(Path.GetTempPath(), $"moorestech-pending-{Guid.NewGuid():N}");
            _snapshots = Path.Combine(_root, "snapshots");
            _lastSession = Path.Combine(_root, "last-session");
            Directory.CreateDirectory(_snapshots);
        }

        [TearDown]
        public void DeleteDirectories()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void 異常終了を検知すると未応答の印が置かれる()
        {
            PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, "session_1", false, null)));

            Assert.IsTrue(PendingCrashReportMark.IsPending(_lastSession));
        }

        // ゲートを出さない起動の後に正常起動が来ても、答えるまでは前世代を消さずに同じ資料をもう一度出す
        // Even when a clean boot follows a launch that showed no gate, the older generation stays and the same evidence is shown again until answered
        [Test]
        public void 未応答の間は正常終了でも前世代を消さず再提示する()
        {
            var previousGeneration = Path.Combine(_lastSession, "recording", "pid_777", "session_1");
            Directory.CreateDirectory(previousGeneration);
            File.WriteAllText(Path.Combine(previousGeneration, "segment-0.mp4"), "old");
            PendingCrashReportMark.MarkPending(_lastSession);

            var artifacts = PreviousSessionSalvage.Salvage(Request(Session(DeadProcessId, "session_2", true, null)));

            Assert.IsFalse(artifacts.PreviousExitWasClean, "未応答の資料があるのにゲートを出さない判定になっている");
            Assert.AreEqual(Path.Combine(_lastSession, "recording"), artifacts.RecordingDirectory);
            Assert.IsTrue(File.Exists(Path.Combine(previousGeneration, "segment-0.mp4")), "未応答の前世代が消えている");
            Assert.IsTrue(PendingCrashReportMark.IsPending(_lastSession), "答えていないのに未応答の印が消えている");
        }

        // 落ちたセッションの出所は last-session へ残り、後の起動で再提示するときも同じ値を載せる（F12）
        // The crashed session's origin stays in last-session, and a later boot re-presenting the evidence carries the same value (F12)
        [Test]
        public void 異常終了セッションの出所は再提示の起動でも読み戻せる()
        {
            var origin = new SessionOriginSnapshot("steam-crashed", null, BuildOriginReading.Editor());
            var crashed = Session(DeadProcessId, "session_1", false, null);
            crashed.Origin = origin;

            var first = PreviousSessionSalvage.Salvage(Request(crashed));
            Assert.AreEqual("steam-crashed", first.PreviousOrigin.SteamId);

            var carried = PreviousSessionSalvage.Salvage(Request(Session(OtherDeadProcessId, "session_2", true, null)));
            Assert.AreEqual("steam-crashed", carried.PreviousOrigin.SteamId, "再提示で出所が失われている");
        }

        [Test]
        public void 出所が読めない異常終了は欠損として表明する()
        {
            var crashed = Session(DeadProcessId, "session_1", false, null);
            crashed.OriginMissingReason = "印が無い";

            var artifacts = PreviousSessionSalvage.Salvage(Request(crashed));

            Assert.IsNull(artifacts.PreviousOrigin);
            Assert.IsTrue(artifacts.Missing.Exists(item => item.Item == "previousOrigin" && item.Reason.Contains("印が無い")));
        }

        // 同じpidに複数セッションがあれば、1つでも異常終了ならそのpidは異常終了として畳む（F19）
        // With several sessions under one pid, a single unclean one folds that pid to unclean (F19)
        [Test]
        public void pidごとの終了状態は同じpidの全セッションを畳んだ結果になる()
        {
            var artifacts = PreviousSessionSalvage.Salvage(Request(
                Session(DeadProcessId, "session_1", true, null),
                Session(DeadProcessId, "session_2", false, null),
                Session(OtherDeadProcessId, "session_3", true, null)));

            Assert.IsFalse(artifacts.ExitedCleanlyByProcessId[DeadProcessId]);
            Assert.IsTrue(artifacts.ExitedCleanlyByProcessId[OtherDeadProcessId]);
        }

        [Test]
        public void 正常終了の退避結果は送る物を持たずゲートを出さない()
        {
            var artifacts = PreviousSessionArtifacts.Clean(_lastSession, new Dictionary<int, bool>(), new List<MissingItem>());

            Assert.IsTrue(artifacts.PreviousExitWasClean);
            Assert.IsFalse(artifacts.HasAnythingToSend);
            Assert.IsNull(artifacts.PreviousOrigin);
        }

        private static PreviousProcessSession Session(int processId, string sessionName, bool exitedCleanly, string recordingDirectory)
        {
            return new PreviousProcessSession { ProcessId = processId, SessionName = sessionName, ExitedCleanly = exitedCleanly, RecordingDirectory = recordingDirectory };
        }

        private PreviousSessionSalvageRequest Request(params PreviousProcessSession[] sessions)
        {
            return new PreviousSessionSalvageRequest
            {
                LastSessionDirectory = _lastSession,
                PreviousSessions = new List<PreviousProcessSession>(sessions),
            };
        }
    }
}
