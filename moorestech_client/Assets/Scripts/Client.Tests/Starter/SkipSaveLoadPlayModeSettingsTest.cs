using Client.Starter;
using Client.Starter.Editor;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Tests.Starter
{
    // SkipSaveLoadPlayModeがAutoSaveと常時記録を無効のまま起動させ続けることの回帰ガード
    // Regression guard that SkipSaveLoadPlayMode keeps booting with auto-save off and always-on capture disabled
    public class SkipSaveLoadPlayModeSettingsTest
    {
        [SetUp]
        public void SetUp()
        {
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, false);
        }

        [TearDown]
        public void TearDown()
        {
            // フラグ残置は後続テストの起動引数を汚染するため必ず戻す
            // A leftover flag pollutes launch args of later tests, so always reset it
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, false);
        }

        [Test]
        public void フラグ有効時はAutoSaveを無効化し常時記録も無効のままにする()
        {
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, true);
            var proprieties = InitializeProprieties.CreateLocalServer(null);

            SkipSaveLoadPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.AutoSave, Is.False);

            // 常時記録は本番のプレイ開始だけが有効にするので、この経路を通っても無効のまま
            // Only the real play start enables always-on capture, so this path leaves it disabled
            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.False);
        }

        [Test]
        public void フラグ無効時は起動引数を変更しない()
        {
            var proprieties = InitializeProprieties.CreateLocalServer(null);
            var original = new StartServerSettings
            {
                WorldDirectory = "/tmp/moorestech-test-world",
                AutoSave = true,
            };
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(original);

            SkipSaveLoadPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/moorestech-test-world"));
            Assert.That(settings.AutoSave, Is.True);
        }
    }
}
