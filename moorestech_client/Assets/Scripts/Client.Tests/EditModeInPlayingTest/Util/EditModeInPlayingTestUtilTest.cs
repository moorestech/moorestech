using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // EditModeInPlayingTestが起動する内蔵サーバーがAutoSave/CaptureRing/録画リングを無効化し続けることの回帰ガード
    // Regression guard that the embedded server EditModeInPlayingTest boots keeps AutoSave, CaptureRing and the recording ring disabled
    public class EditModeInPlayingTestUtilTest
    {
        [TearDown]
        public void TearDown()
        {
            // 後続テストの起動へ無効化状態が漏れないよう毎回戻す
            // Reset so the disabled state never leaks into a later test's boot
            BugReportRecordingSettings.SetEnabled(true);
        }

        [Test]
        public void CreateServerSettings_オートセーブと常時記録を無効化する()
        {
            var settings = EditModeInPlayingTestUtil.CreateServerSettings("/tmp/world", "/tmp/server", "template");

            Assert.That(settings.AutoSave, Is.False);
            Assert.That(settings.CaptureRing, Is.False);
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/world"));
            Assert.That(settings.ServerDataDirectory, Is.EqualTo("/tmp/server"));
            Assert.That(settings.MapMode, Is.EqualTo("template"));

            // 録画リングもCaptureRingと同じ役割で無効化されているはず
            // The recording ring should be disabled in the same role as CaptureRing
            Assert.That(BugReportRecordingSettings.Enabled, Is.False);
        }
    }
}
