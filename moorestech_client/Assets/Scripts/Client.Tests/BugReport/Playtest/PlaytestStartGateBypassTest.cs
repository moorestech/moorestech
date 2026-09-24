using Client.Game.InGame.BugReport.Playtest;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport
{
    // 無人起動の印は覗いても残り、ゲートが読んだときだけ消費され、その答えは起動中ラッチされることを押さえる
    // Pins that the unattended boot mark survives a peek, is consumed only when the gate reads it, and that answer is latched for the boot
    public class PlaytestStartGateBypassTest
    {
        private const string UnattendedBootKey = "PlaytestStartGateBypass_UnattendedBoot";

        [SetUp]
        public void SetUp()
        {
            // 消費実装の退行時も印を後続へ漏らさない。ラッチも再生し直しと同じ入口で戻す
            // Prevent mark leaks even when the consumption implementation regresses; the latch is reset through the same entry a replay uses
            UnityEditor.SessionState.EraseBool(UnattendedBootKey);
            PlaytestStartGateBypass.ResetOnPlayMode();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEditor.SessionState.EraseBool(UnattendedBootKey);
            PlaytestStartGateBypass.ResetOnPlayMode();
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
            if (!Application.isBatchMode) Assert.That(peeked, Is.EqualTo("unattendedBootMark"));
        }

        // 印の消費後に読む側（退避のログ・常時記録の判定）が別の答えを得ると、同じ起動の無人判定が食い違う
        // If readers after the consumption (salvage log, capture decision) got another answer, one boot's unattended decision would disagree with itself
        [Test]
        public void 無人起動の理由は初回の解決でラッチされ再生し直しで戻る()
        {
            PlaytestStartGateBypass.Apply();
            var first = PlaytestStartGateBypass.UnattendedReason();

            Assert.IsNotNull(first);
            Assert.That(UnityEditor.SessionState.GetBool(UnattendedBootKey, false), Is.False, "初回の解決が印を消費していない");
            Assert.AreEqual(first, PlaytestStartGateBypass.UnattendedReason(), "2回目の読み取りで理由が変わっている");
            Assert.AreEqual(first, PlaytestStartGateBypass.PeekUnattendedReason(), "消費後に覗くと理由が変わっている");

            // 再生し直しは印の無い新しい起動なので、ラッチを戻せば有人になる
            // A replay is a fresh boot with no mark, so resetting the latch makes it attended
            PlaytestStartGateBypass.ResetOnPlayMode();
            if (!Application.isBatchMode) Assert.IsNull(PlaytestStartGateBypass.UnattendedReason());
        }

        // タイトルを通らない起動の宣言は理由付きで残り、再生し直しで消える
        // A direct-boot declaration is kept with its reason and cleared by a replay
        [Test]
        public void 直接起動の宣言は理由を残し再生し直しで消える()
        {
            Assert.IsNull(PlaytestStartGateBypass.DirectBootReason());

            PlaytestStartGateBypass.DeclareDirectBoot("test direct boot");
            Assert.AreEqual("test direct boot", PlaytestStartGateBypass.DirectBootReason());

            PlaytestStartGateBypass.ResetOnPlayMode();
            Assert.IsNull(PlaytestStartGateBypass.DirectBootReason());
        }
    }
}
