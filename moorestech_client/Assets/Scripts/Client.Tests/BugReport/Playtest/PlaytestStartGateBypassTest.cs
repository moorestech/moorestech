using Client.Game.InGame.BugReport.Playtest;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport
{
    // 無人起動の印は覗いても残り、ゲートが読んだときだけ消費されることを押さえる
    // Pins that the unattended boot mark survives a peek and is consumed only when the gate reads it
    public class PlaytestStartGateBypassTest
    {
        private const string UnattendedBootKey = "PlaytestStartGateBypass_UnattendedBoot";

        [SetUp]
        public void SetUp()
        {
            // 消費実装の退行時も印を後続へ漏らさない
            // Prevent mark leaks even when the consumption implementation regresses
            UnityEditor.SessionState.EraseBool(UnattendedBootKey);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEditor.SessionState.EraseBool(UnattendedBootKey);
        }

        // 常時記録の判定は開始ゲートより先に走る。覗いただけで印が消えると、後から読むゲートが応答待ちで恒久停止する
        // The capture decision runs before the start gates; if peeking erased the mark, the gates reading later would wait forever
        [Test]
        public void 無人起動の理由は覗いても消費されない()
        {
            PlaytestStartGateBypass.Apply();
            Assert.That(UnityEditor.SessionState.GetBool(UnattendedBootKey, false), Is.True);

            var peeked = PlaytestStartGateBypass.PeekUnattendedReason();
            Assert.That(UnityEditor.SessionState.GetBool(UnattendedBootKey, false), Is.True, "Peekが印を消費している");
            var consumed = PlaytestStartGateBypass.UnattendedReason();
            Assert.That(UnityEditor.SessionState.GetBool(UnattendedBootKey, false), Is.False, "ゲートが印を消費していない");

            // 印の副作用を実行環境と分けて検証する
            // Verify mark side effects separately from the environment
            Assert.IsNotNull(peeked);
            Assert.AreEqual(peeked, consumed, "覗いた後にゲートが読む理由が変わっている");
            if (!Application.isBatchMode)
            {
                Assert.That(peeked, Is.EqualTo("unattendedBootMark"));
                Assert.That(PlaytestStartGateBypass.PeekUnattendedReason(), Is.Null);
            }
        }
    }
}
