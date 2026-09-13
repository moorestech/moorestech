using NUnit.Framework;
using Server.Boot;

namespace Client.Tests.BugReport
{
    // 常時記録の可否が起動経路ごとに複製されていた頃の回帰ガード。決定は1つで、既定は無効であること
    // Regression guard from when the always-on capture switch was duplicated per boot path; one decision, disabled by default
    public class AlwaysOnCaptureSettingTest
    {
        // 既定が有効だと、テスト・プレイテスト経路が1行書き忘れるだけで無断で録り始める
        // An enabled default lets a single forgotten line in a test or playtest path start recording unannounced
        [Test]
        public void 既定は無効で明示的に有効化したときだけ録る()
        {
            Assert.That(AlwaysOnCaptureSetting.Disabled().IsEnabled, Is.False);
            Assert.That(AlwaysOnCaptureSetting.Enabled().IsEnabled, Is.True);
            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.False, "テスト起動では常時記録が無効のままであること");
        }
    }
}
