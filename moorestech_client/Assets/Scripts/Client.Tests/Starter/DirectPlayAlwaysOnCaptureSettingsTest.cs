using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter;
using Client.Starter.Editor;
using NUnit.Framework;
using Server.Boot;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Starter
{
    // 直Playの有人時記録を守る（ADR 0066）
    // Guards attended direct-play capture (ADR 0066)
    public class DirectPlayAlwaysOnCaptureSettingsTest
    {
        private const string UnattendedBootKey = "PlaytestStartGateBypass_UnattendedBoot";

        [SetUp]
        public void SetUp()
        {
            SessionState.EraseBool(UnattendedBootKey);
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
        }

        [TearDown]
        public void TearDown()
        {
            // 静的な決定の残置は後続テストの起動を録り始めさせるため必ず戻す
            // A leftover static decision would make later test boots start recording, so always reset it
            SessionState.EraseBool(UnattendedBootKey);
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
        }

        [Test]
        public void 有人の直Playは常時記録を有効にする()
        {
            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason(null);

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.True);
        }

        [Test]
        public void 無人起動は常時記録を有効にせず理由をログへ出す()
        {
            LogAssert.Expect(LogType.Log, new Regex("無人起動のため直Playの常時記録を自動では有効にしません reason:unattendedBootMark"));

            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason("unattendedBootMark");

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.False);
        }

        // 無人起動でも記録したいテストは起動前に明示的に有効化する。それを無効へ上書きしてはいけない
        // A test that wants capture on an unattended boot enables it up front; this must never overwrite that back to disabled
        [Test]
        public void 無人起動でも事前の明示的な有効化は潰さない()
        {
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());

            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason("batchMode");

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.True);
        }

        // 接続方式を問わず上位入口を通す
        // Exercise the upper entry for every connection mode
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void 起動入口は無人理由をログへ出し既存設定と印を保つ(bool remote, bool enabled)
        {
            AlwaysOnCaptureSetting.Apply(enabled ? AlwaysOnCaptureSetting.Enabled() : AlwaysOnCaptureSetting.Disabled());
            PlaytestStartGateBypass.Apply();
            var proprieties = remote
                ? InitializeProprieties.CreateRemoteConnection("127.0.0.1", 25565, 1)
                : InitializeProprieties.CreateLocalServer(null);

            // 実行環境の理由と印の保持を別々に確認する
            // Verify the environment reason and mark retention separately
            var reason = Application.isBatchMode ? "batchMode" : "unattendedBootMark";
            LogAssert.Expect(LogType.Log, new Regex($"無人起動のため直Playの常時記録を自動では有効にしません reason:{reason}"));
            PlayModeLaunchOverrides.ApplyIfNeeded(proprieties);

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.EqualTo(enabled));
            Assert.That(SessionState.GetBool(UnattendedBootKey, false), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 印なしの起動入口は環境の有人判定に従う(bool remote)
        {
            var proprieties = remote
                ? InitializeProprieties.CreateRemoteConnection("127.0.0.1", 25565, 1)
                : InitializeProprieties.CreateLocalServer(null);
            if (Application.isBatchMode)
            {
                LogAssert.Expect(LogType.Log, new Regex("無人起動のため直Playの常時記録を自動では有効にしません reason:batchMode"));
            }

            // CIのbatchModeを無視しない
            // Do not ignore batchMode in CI
            PlayModeLaunchOverrides.ApplyIfNeeded(proprieties);
            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.EqualTo(!Application.isBatchMode));
        }
    }
}
