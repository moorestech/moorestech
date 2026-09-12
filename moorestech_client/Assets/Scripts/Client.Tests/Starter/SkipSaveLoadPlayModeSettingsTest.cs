using Client.Game.InGame.BugReport.Recording;
using Client.Starter;
using Client.Starter.Editor;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Tests.Starter
{
    // SkipSaveLoadPlayModeがAutoSave/CaptureRing/録画リングを無効化し続けることの回帰ガード
    // Regression guard that SkipSaveLoadPlayMode keeps disabling AutoSave, CaptureRing and the recording ring
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
            BugReportRecordingSettings.SetEnabled(true);
        }

        [Test]
        public void フラグ有効時はAutoSaveとCaptureRingを無効化する()
        {
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, true);
            var proprieties = InitializeProprieties.CreateLocalServer(null);

            SkipSaveLoadPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.AutoSave, Is.False);
            Assert.That(settings.CaptureRing, Is.False);

            // 録画リングもCaptureRingと同じ役割で無効化されているはず
            // The recording ring should be disabled in the same role as CaptureRing
            Assert.That(BugReportRecordingSettings.Enabled, Is.False);
        }

        [Test]
        public void フラグ無効時は起動引数を変更しない()
        {
            var proprieties = InitializeProprieties.CreateLocalServer(null);
            var original = new StartServerSettings
            {
                WorldDirectory = "/tmp/moorestech-test-world",
                AutoSave = true,
                CaptureRing = true,
            };
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(original);

            SkipSaveLoadPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/moorestech-test-world"));
            Assert.That(settings.AutoSave, Is.True);
            Assert.That(settings.CaptureRing, Is.True);
        }
    }
}
