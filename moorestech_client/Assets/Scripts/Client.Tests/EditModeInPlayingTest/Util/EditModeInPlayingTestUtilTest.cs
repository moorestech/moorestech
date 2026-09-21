using System.Collections;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter;
using Client.Starter.Editor;
using NUnit.Framework;
using Server.Boot;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // EditModeInPlayingTestが起動する内蔵サーバーがAutoSaveと常時記録を無効のまま走り続けることの回帰ガード
    // Regression guard that the embedded server EditModeInPlayingTest boots keeps auto-save off and always-on capture disabled
    public class EditModeInPlayingTestUtilTest
    {
        private const string UnattendedBootKey = "PlaytestStartGateBypass_UnattendedBoot";

        [TearDown]
        public void TearDown()
        {
            SessionState.EraseBool(UnattendedBootKey);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void CreateServerSettings_オートセーブと常時記録を無効化する()
        {
            var settings = EditModeInPlayingTestUtil.CreateServerSettings("/tmp/world", "/tmp/server", "template");

            Assert.That(settings.AutoSave, Is.False);
            Assert.That(settings.WorldDirectory, Is.EqualTo("/tmp/world"));
            Assert.That(settings.ServerDataDirectory, Is.EqualTo("/tmp/server"));
            Assert.That(settings.MapMode, Is.EqualTo("template"));

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.False);
        }

        [UnityTest]
        public IEnumerator ゲート未到達でPlayを終了しても次の有人起動へ印を残さない()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            // ゲート未起動でも終了時に印を失効する
            // Expire the mark on exit even before the gate starts
            Assert.That(SessionState.GetBool(UnattendedBootKey, false), Is.True);
            yield return new ExitPlayMode();
            Assert.That(SessionState.GetBool(UnattendedBootKey, false), Is.False);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            // 印なしで同じ起動入口を通す
            // Reuse the same launch entry without a mark
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            var reason = PlaytestStartGateBypass.PeekUnattendedReason();
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
            PlayModeLaunchOverrides.ApplyIfNeeded(InitializeProprieties.CreateLocalServer(null));
            var enabled = AlwaysOnCaptureSetting.Current.IsEnabled;

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            Assert.That(reason, Is.EqualTo(Application.isBatchMode ? "batchMode" : null));
            Assert.That(enabled, Is.EqualTo(!Application.isBatchMode));
        }
    }
}
