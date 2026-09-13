using NUnit.Framework;
using Server.Boot;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // EditModeInPlayingTestが起動する内蔵サーバーがAutoSaveと常時記録を無効のまま走り続けることの回帰ガード
    // Regression guard that the embedded server EditModeInPlayingTest boots keeps auto-save off and always-on capture disabled
    public class EditModeInPlayingTestUtilTest
    {
        [Test]
        public void CreateServerSettings_オートセーブと常時記録を無効化する()
        {
            var settings = EditModeInPlayingTestUtil.CreateServerSettings("/tmp/world", "/tmp/server", "template");

            Assert.That(settings.AutoSave, Is.False);
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/world"));
            Assert.That(settings.ServerDataDirectory, Is.EqualTo("/tmp/server"));
            Assert.That(settings.MapMode, Is.EqualTo("template"));

            // 常時記録は本番のプレイ開始だけが有効にするので、テスト起動では無効のまま
            // Only the real play start enables always-on capture, so a test boot leaves it disabled
            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.False);
        }
    }
}
