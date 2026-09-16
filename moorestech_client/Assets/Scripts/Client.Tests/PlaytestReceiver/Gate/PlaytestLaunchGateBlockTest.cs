using Client.Localization;
using Client.PlaytestReceiver.Gate;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestLaunchGateBlockTest
    {
        // 拒否時はゲートが理由の文言まで解決するので、EditModeでも辞書を読み込んでおく
        // A refusal resolves its reason text inside the gate, so the dictionary is loaded even in EditMode
        [SetUp]
        public void LoadDictionary()
        {
            Localize.Initialize();
        }

        [TearDown]
        public void RestoreGate()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);
        }

        [Test]
        public void 未評価でも配布ビルドの印が無ければ開発者モードに確定して通す()
        {
            // Editorには build-info.json が無い。遅延評価で開発者モードに確定し、開始を止めない
            // The Editor has no build-info.json, so the lazy evaluation settles as developer mode and never stops a start
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);

            Assert.IsTrue(PlaytestLaunchGate.TryPassStart("test", out _));
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, PlaytestLaunchGate.Current.Value.Status);
        }

        [Test]
        public void 開発者モードでは関所が拒否しない()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.DeveloperMode);
            Assert.IsTrue(PlaytestLaunchGate.TryPassStart("test", out _));
        }

        [Test]
        public void 止められているときは拒否する()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Blocked(PlaytestGateStatus.Unreachable, "dns-detail-for-logs"));
            Assert.IsFalse(PlaytestLaunchGate.TryPassStart("test", out var denyReasonText));

            // テスター向けの文言は種別から解決し、ログ専用のDetailは混ぜない
            // The tester-facing text is resolved from the kind and never carries the log-only Detail
            Assert.IsFalse(string.IsNullOrEmpty(denyReasonText));
            StringAssert.DoesNotContain("dns-detail-for-logs", denyReasonText);
        }

        [Test]
        public void 照合中も拒否する()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Checking);
            Assert.IsFalse(PlaytestLaunchGate.TryPassStart("test", out _));
        }
    }
}
