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
            var result = PlaytestGateDecision.Decide(false, true, PlaytestSessionOutcome.NotAllowed, "");
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void Steamが動いていなければ自作ビルド直起動として素通しする()
        {
            var result = PlaytestGateDecision.Decide(true, false, PlaytestSessionOutcome.TicketUnavailable, "");
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void 配布ビルドで許可されれば通す()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Allowed, "");
            Assert.AreEqual(PlaytestGateStatus.Allowed, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void 不許可は理由付きで止める()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.NotAllowed, "");
            Assert.AreEqual(PlaytestGateStatus.NotAllowed, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.NotAllowed.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 到達不能は止める()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Unreachable, "dns");
            Assert.AreEqual(PlaytestGateStatus.Unreachable, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.Unreachable.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 配布ビルドでSteamが動いているのにチケットが取れないのは止める()
        {
            var unavailable = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.TicketUnavailable, "");
            var rejected = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.TicketRejected, "");
            Assert.AreEqual(PlaytestGateStatus.TicketFailed, unavailable.Status);
            Assert.AreEqual(PlaytestGateStatus.TicketFailed, rejected.Status);
            Assert.IsTrue(unavailable.IsBlocked);
            Assert.IsTrue(rejected.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.TicketFailed.Key, unavailable.ReasonKey.Key);
        }

        [Test]
        public void 理由の文言に渡すDetailはnullでも空文字になる()
        {
            // ReasonKeyの文言は{p0}を持つ。nullのまま整形へ流すと呼び出し側ごとにnull対策が要る
            // The reason text carries {p0}; leaking null into the formatter would force a null guard at every call site
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Unreachable, null);
            Assert.AreEqual("", result.Detail);
        }
    }
}
