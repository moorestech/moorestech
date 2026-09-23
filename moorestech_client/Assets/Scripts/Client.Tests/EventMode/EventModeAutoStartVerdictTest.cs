using Client.PlaytestReceiver.Gate;
using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    // 出展機の自動開始は起動時照合の確定を待つ。待たずに開始すると漏斗がタイトルへ戻し、起動フックは二度と発火しない（配布版の固着）
    // The exhibition auto start waits for the launch verdict; starting earlier makes the funnel bounce back to the title and the boot hook never fires again (a stuck distribution build)
    public class EventModeAutoStartVerdictTest
    {
        // 確定待ちが未確定のまま返るのは期限切れのときだけ。無期限に待たず、断念の理由を期限切れと名乗る
        // The wait returns an unsettled verdict only past its deadline; it abandons instead of waiting forever and names the timeout as the reason
        [Test]
        public void 未確定のまま返った照合は期限切れとして自動開始を断念する()
        {
            Assert.AreEqual(EventModeAutoStartDecision.AbandonTimeout, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.NotEvaluated));
            Assert.AreEqual(EventModeAutoStartDecision.AbandonTimeout, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Checking));
        }

        [Test]
        public void 照合を通れば自動開始する()
        {
            Assert.AreEqual(EventModeAutoStartDecision.Start, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.DeveloperMode));
            Assert.AreEqual(EventModeAutoStartDecision.Start, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Allowed(null, "7656")));
        }

        [Test]
        public void 照合に止められたら照合による断念とする()
        {
            Assert.AreEqual(EventModeAutoStartDecision.AbandonBlocked, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, "")));
            Assert.AreEqual(EventModeAutoStartDecision.AbandonBlocked, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Blocked(PlaytestGateStatus.Unreachable, "dns")));
        }
    }
}
