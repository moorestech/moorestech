using Client.Game.InGame.BugReport.Recording;
using Client.Playtest;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;

namespace Client.Tests.Playtest
{
    public class PlaytestWorldBootSessionTest
    {
        [TearDown]
        public void TearDown()
        {
            PlaytestWorldBootSession.Clear();

            // 後続テストの起動へ無効化状態が漏れないよう毎回戻す
            // Reset so the disabled state never leaks into a later test's boot
            BugReportRecordingSettings.SetEnabled(true);
        }

        [TestCase(0)]
        [TestCase(12345)]
        public void Save_固定ワールド設定を起動引数へ復元する(int seed)
        {
            // 固定値をセッションへ保存し、domain reload後と同じ復元経路を通す
            // Save fixed values into the session and use the same restore path as after domain reload
            PlaytestWorldBootSession.Save("/master/server_v8", "/tmp/playtest-world", "generated", seed);

            var restored = PlaytestWorldBootSession.TryCreateInitializeProprieties(out var proprieties);

            // 全起動引数とAutoSave無効化が欠落なく維持されることを固定する
            // Pin every boot argument and the disabled auto-save setting without omissions
            Assert.That(restored, Is.True);
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.ServerDataDirectory, Is.EqualTo("/master/server_v8"));
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/playtest-world"));
            Assert.That(settings.MapMode, Is.EqualTo("generated"));
            Assert.That(settings.Seed, Is.EqualTo(seed));
            Assert.That(settings.AutoSave, Is.False);
            Assert.That(settings.CaptureRing, Is.False);

            // 録画リングもCaptureRingと同じ役割で無効化されているはず
            // The recording ring should be disabled in the same role as CaptureRing
            Assert.That(BugReportRecordingSettings.Enabled, Is.False);
        }

        [Test]
        public void Clear_保存済み起動設定を完全に破棄する()
        {
            // 前回セッションの設定を保存してから終了時cleanupを模擬する
            // Save the previous session settings, then simulate edit-mode cleanup
            PlaytestWorldBootSession.Save("/master/server_v8", "/tmp/playtest-world", "generated", 67890);
            PlaytestWorldBootSession.Clear();

            var restored = PlaytestWorldBootSession.TryCreateInitializeProprieties(out _);

            // 次回の通常再生へ固定world設定が漏れないことを保証する
            // Ensure fixed-world settings never leak into the next normal play session
            Assert.That(restored, Is.False);
        }
    }
}
