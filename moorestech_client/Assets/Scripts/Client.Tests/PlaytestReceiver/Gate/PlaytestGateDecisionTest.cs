using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestGateDecisionTest
    {
        [Test]
        public void buildInfoが無ければ開発者モードで素通しする()
        {
            var result = PlaytestGateDecision.Decide(false, true, PlaytestSessionOutcome.NotAllowed, "", null);
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void Steamが動いていなければ自作ビルド直起動として素通しする()
        {
            var result = PlaytestGateDecision.Decide(true, false, PlaytestSessionOutcome.TicketUnavailable, "", null);
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void 配布ビルドで許可されれば通しセッションを渡す()
        {
            var session = new PlaytestSession(new FakeApi(), new FakeTicketProvider("aabb"));
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Allowed, "", session);
            Assert.AreEqual(PlaytestGateStatus.Allowed, result.Status);
            Assert.IsFalse(result.IsBlocked);
            Assert.IsTrue(result.TryGetAllowedSession(out var allowedSession));
            Assert.AreSame(session, allowedSession);
        }

        [Test]
        public void 不許可は理由付きで止めセッションを渡さない()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.NotAllowed, "", null);
            Assert.AreEqual(PlaytestGateStatus.NotAllowed, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.IsFalse(result.TryGetAllowedSession(out _));
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.NotAllowed.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 到達不能は止める()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Unreachable, "dns", null);
            Assert.AreEqual(PlaytestGateStatus.Unreachable, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.Unreachable.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 配布ビルドでSteamが動いているのにチケットが取れないのは止める()
        {
            var unavailable = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.TicketUnavailable, "", null);
            var rejected = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.TicketRejected, "", null);
            Assert.AreEqual(PlaytestGateStatus.TicketFailed, unavailable.Status);
            Assert.AreEqual(PlaytestGateStatus.TicketFailed, rejected.Status);
            Assert.IsTrue(unavailable.IsBlocked);
            Assert.IsTrue(rejected.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.TicketFailed.Key, unavailable.ReasonKey.Key);
        }

        [Test]
        public void 照合中と未評価は止まっていて待ち文言を理由に出す()
        {
            // 判定が出るまでの暫定値。ここがIsBlockedでないと待ち時間がそのまま素通しの窓になる
            // The provisional verdicts; if they were not blocked the wait itself would become a bypass window
            foreach (var provisional in new[] { PlaytestGateResult.Checking, PlaytestGateResult.NotEvaluated })
            {
                Assert.IsTrue(provisional.IsBlocked, provisional.Status.ToString());
                Assert.AreEqual(LocalizationKeys.Ui.Playtest.Checking.Key, provisional.ReasonKey.Key);
            }
        }

        [Test]
        public void ログ用のDetailはnullでも空文字になる()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Unreachable, null, null);
            Assert.AreEqual("", result.Detail);
        }
    }
}
