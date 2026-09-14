using System.IO;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class CleanExitMarkerTest
    {
        // 実プロセスと衝突しないpid。マーカーはマシン共通のBugReports/配下に置かれるため必ず後始末する
        // A pid that cannot collide with a real process; the marks live under the machine-wide BugReports/, so they are always cleaned up
        private const int TestProcessId = 999001;

        [SetUp]
        [TearDown]
        public void RemoveMarkers()
        {
            GameShutdownEvent.ResetForNewSession();
            if (File.Exists(CleanExitMarker.CleanMarkerPath(TestProcessId))) File.Delete(CleanExitMarker.CleanMarkerPath(TestProcessId));
            if (File.Exists(CleanExitMarker.SessionMarkerPath(TestProcessId))) File.Delete(CleanExitMarker.SessionMarkerPath(TestProcessId));
        }

        [Test]
        public void マーカーがあれば前回は正常終了と判定し印ごと消える()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId);
            CleanExitMarker.MarkCleanExit(TestProcessId);

            Assert.IsTrue(CleanExitMarker.ConsumeExitCleanFlag(TestProcessId));
            Assert.IsFalse(File.Exists(CleanExitMarker.CleanMarkerPath(TestProcessId)));
            Assert.IsFalse(File.Exists(CleanExitMarker.SessionMarkerPath(TestProcessId)));
        }

        [Test]
        public void 生存印だけが残っていれば前回は異常終了と判定する()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId);

            CollectionAssert.Contains(CleanExitMarker.SessionProcessIds(), TestProcessId);
            Assert.IsFalse(CleanExitMarker.ConsumeExitCleanFlag(TestProcessId));
        }

        // 生きているpidの印を消すと、そのEditorが本当に落ちても次回起動で検知できない（印が1つも残らないため）
        // Erasing a live pid's marks would leave its real crash undetectable at the next boot, since no mark survives it
        [Test]
        public void 生存しているpidの印は回収の対象にならず消えない()
        {
            CleanExitMarker.MarkSessionStarted(TestProcessId);

            var scan = PreviousProcessScanner.Scan(0, new RecordingProcessTakeover(), CleanExitMarker.SessionProcessIds(), new[] { TestProcessId });
            foreach (var session in scan.Sessions) CleanExitMarker.ConsumeExitCleanFlag(session.ProcessId);

            Assert.Contains(TestProcessId, scan.SkippedLiveProcessIds);
            Assert.IsTrue(File.Exists(CleanExitMarker.SessionMarkerPath(TestProcessId)));
        }

        [Test]
        public void 意図的な終了だけが正常終了マーカーを書く()
        {
            CleanExitMarkWriter.InstallAtStartup(TestProcessId);

            // 初期化失敗でメインメニューへ戻る経路は、拾いたいクラッシュ側。ここで印を書くと録画が次回起動で捨てられる
            // The fold-up to the main menu after a failed initialization is the crash side; a mark here would discard the recording at the next boot
            GameShutdownEvent.FireGameShutdown(GameShutdownReason.InitializationFailed);
            Assert.IsFalse(File.Exists(CleanExitMarker.CleanMarkerPath(TestProcessId)));

            GameShutdownEvent.ResetForNewSession();
            GameShutdownEvent.FireGameShutdown(GameShutdownReason.IntentionalExit);
            Assert.IsTrue(File.Exists(CleanExitMarker.CleanMarkerPath(TestProcessId)));
        }
    }
}
