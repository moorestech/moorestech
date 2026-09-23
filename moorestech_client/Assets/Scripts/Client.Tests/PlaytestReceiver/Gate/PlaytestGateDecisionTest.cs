using System.Text.RegularExpressions;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestGateDecisionTest
    {
        [Test]
        public void buildInfoが無ければ開発者モードで素通しする()
        {
            var result = PlaytestGateDecision.Decide(false, true, Authenticated(PlaytestSessionOutcome.NotAllowed, "", null), null);
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void Steamが動いていなければ自作ビルド直起動として素通しする()
        {
            var result = PlaytestGateDecision.Decide(true, false, Authenticated(PlaytestSessionOutcome.TicketUnavailable, "", null), null);
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void 配布ビルドで許可されれば通しセッションを渡す()
        {
            var session = new PlaytestSession(new FakeApi(), new FakeTicketProvider("aabb"));
            var result = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.Allowed, "", "7656"), session);
            Assert.AreEqual(PlaytestGateStatus.Allowed, result.Status);
            Assert.IsFalse(result.IsBlocked);
            Assert.IsTrue(result.TryGetAllowedSession(out var allowedSession));
            Assert.AreSame(session, allowedSession);
            Assert.IsTrue(result.TryGetVerifiedSteamId(out var verifiedSteamId));
            Assert.AreEqual("7656", verifiedSteamId);
        }

        [Test]
        public void 不許可は理由付きで止めセッションを渡さない()
        {
            var result = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.NotAllowed, "", null), null);
            Assert.AreEqual(PlaytestGateStatus.NotAllowed, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.IsFalse(result.TryGetAllowedSession(out _));
            Assert.IsFalse(result.TryGetVerifiedSteamId(out _));
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.NotAllowed.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 到達不能は止める()
        {
            var result = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.Unreachable, "dns", null), null);
            Assert.AreEqual(PlaytestGateStatus.Unreachable, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.Unreachable.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 契約違反の応答は到達不能と別の理由で止める()
        {
            var result = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.MalformedResponse, "html", null), null);
            Assert.AreEqual(PlaytestGateStatus.MalformedResponse, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.MalformedResponse.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 配布ビルドでSteamが動いているのにチケットが取れないのは止める()
        {
            var unavailable = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.TicketUnavailable, "", null), null);
            var rejected = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.TicketRejected, "", null), null);
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

        // トークンのキャッシュ短絡など、SteamIDを載せないAllowedが将来Decideへ届いても、識別が空のまま通らないことを押さえる
        // Pins that a future Allowed without a SteamID (e.g. the token cache short-circuit) never passes with an empty identity
        [Test]
        public void 検証済みSteamIDの無いAllowedは契約違反として止める()
        {
            foreach (var emptySteamId in new[] { null, "" })
            {
                LogAssert.Expect(LogType.Error, new Regex("allowed without a verified steamId"));
                var result = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.Allowed, "", emptySteamId), null);
                Assert.IsTrue(result.IsBlocked);
                Assert.AreEqual(PlaytestGateStatus.MalformedResponse, result.Status);
                Assert.IsFalse(result.TryGetVerifiedSteamId(out _));
            }
        }

        [Test]
        public void ログ用のDetailはnullでも空文字になる()
        {
            var result = PlaytestGateDecision.Decide(true, true, Authenticated(PlaytestSessionOutcome.Unreachable, null, null), null);
            Assert.AreEqual("", result.Detail);
        }

        private static PlaytestSessionResult Authenticated(PlaytestSessionOutcome outcome, string detail, string steamId)
        {
            return new PlaytestSessionResult { Outcome = outcome, Detail = detail, SteamId = steamId };
        }
    }
}
