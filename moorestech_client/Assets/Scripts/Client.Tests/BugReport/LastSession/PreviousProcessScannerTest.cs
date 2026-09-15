using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 前回セッションの選別。生存判定を1つの集合で行い、自pidは今回のセッションだけを除く
    // The previous-session selection: liveness comes from one set, and for this pid only the current session is excluded
    public class PreviousProcessScannerTest
    {
        private const int DeadProcessId = 1234;
        private const int LiveProcessId = 5678;
        private const int CurrentProcessId = 4321;
        private const string OlderSessionName = "session_100";
        private const string CurrentSessionName = "session_200";

        // 常時記録オフやffmpeg不在のEditorは録画ディレクトリを作らない。録画の有無で生存を判定すると、この印が消えて偽のクラッシュになる
        // An Editor with capture off or no ffmpeg creates no recording directory; judging liveness by that would erase its mark and fabricate a crash
        [Test]
        public void 録画が無くても生存しているpidは前回セッションに数えない()
        {
            var marked = new[] { Marked(LiveProcessId, OlderSessionName), Marked(DeadProcessId, OlderSessionName) };
            var scan = PreviousProcessScanner.Scan(CurrentProcessId, CurrentSessionName, new RecordingProcessTakeover(), marked, new[] { LiveProcessId, CurrentProcessId });

            Assert.AreEqual(1, scan.Sessions.Count);
            Assert.AreEqual(DeadProcessId, scan.Sessions[0].ProcessId);
            Assert.Contains(LiveProcessId, scan.SkippedLiveProcessIds);
        }

        [Test]
        public void 自分のpidの今回のセッションは印があっても前回にしない()
        {
            var scan = PreviousProcessScanner.Scan(CurrentProcessId, CurrentSessionName, new RecordingProcessTakeover(), new[] { Marked(CurrentProcessId, CurrentSessionName) }, new[] { CurrentProcessId });

            Assert.AreEqual(0, scan.Sessions.Count);
            Assert.AreEqual(0, scan.SkippedLiveProcessIds.Count);
        }

        // 同じpidでの再生し直し。pidが生きているからと旧セッションを残すと、その終了状態が次のセッションへ持ち越される（F05）
        // A same-pid replay: keeping the older session because the pid is alive would carry its exit state into the next session (F05)
        [Test]
        public void 自分のpidでも今回以外のセッションは前回として数える()
        {
            var scan = PreviousProcessScanner.Scan(CurrentProcessId, CurrentSessionName, new RecordingProcessTakeover(), new[] { Marked(CurrentProcessId, OlderSessionName) }, new[] { CurrentProcessId });

            Assert.AreEqual(1, scan.Sessions.Count);
            Assert.AreEqual(OlderSessionName, scan.Sessions[0].SessionName);
        }

        // 録画と印の両方を持つセッションは1件として数える。二重に数えると同じ印を2回消費しようとする
        // A session with both a recording and marks counts once; counting it twice would try to consume the same marks twice
        [Test]
        public void 録画と印を両方持つセッションは1件として数える()
        {
            var takeover = new RecordingProcessTakeover();
            takeover.Directories.Add(new RecordingProcessDirectory { ProcessId = DeadProcessId, SessionName = OlderSessionName, Path = "/tmp/recording" });

            var scan = PreviousProcessScanner.Scan(CurrentProcessId, CurrentSessionName, takeover, new[] { Marked(DeadProcessId, OlderSessionName) }, new[] { CurrentProcessId });

            Assert.AreEqual(1, scan.Sessions.Count);
            Assert.AreEqual("/tmp/recording", scan.Sessions[0].RecordingDirectory);
        }

        private static MarkedSession Marked(int processId, string sessionName)
        {
            return new MarkedSession { ProcessId = processId, SessionName = sessionName };
        }
    }
}
