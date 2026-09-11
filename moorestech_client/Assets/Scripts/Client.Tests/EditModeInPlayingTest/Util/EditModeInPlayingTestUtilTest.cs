using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // EditModeInPlayingTestが起動する内蔵サーバーがAutoSave/CaptureRingを無効化し続けることの回帰ガード
    // Regression guard that the embedded server EditModeInPlayingTest boots keeps AutoSave and CaptureRing disabled
    public class EditModeInPlayingTestUtilTest
    {
        [Test]
        public void CreateServerSettings_オートセーブと常時記録を無効化する()
        {
            var settings = EditModeInPlayingTestUtil.CreateServerSettings("/tmp/world", "/tmp/server", "template");

            Assert.That(settings.AutoSave, Is.False);
            Assert.That(settings.CaptureRing, Is.False);
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/world"));
            Assert.That(settings.ServerDataDirectory, Is.EqualTo("/tmp/server"));
            Assert.That(settings.MapMode, Is.EqualTo("template"));
        }
    }
}
