using Client.PlaytestReceiver.Gate;
using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    // 出展機の自動開始は起動時照合の確定を待つ。待たずに開始すると漏斗がタイトルへ戻し、起動フックは二度と発火しない（配布版の固着）
    // The exhibition auto start waits for the launch verdict; starting earlier makes the funnel bounce back to the title and the boot hook never fires again (a stuck distribution build)
    public class EventModeAutoStartVerdictTest
    {
        [Test]
        public void 照合が確定するまでは自動開始しない()
        {
            Assert.AreEqual(EventModeAutoStartDecision.WaitForVerdict, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.NotEvaluated));
            Assert.AreEqual(EventModeAutoStartDecision.WaitForVerdict, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Checking));
        }

        [Test]
        public void 照合を通れば自動開始する()
        {
            Assert.AreEqual(EventModeAutoStartDecision.Start, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.DeveloperMode));
            Assert.AreEqual(EventModeAutoStartDecision.Start, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Allowed(null, "7656")));
        }

        [Test]
        public void 照合に止められたら自動開始を断念する()
        {
            Assert.AreEqual(EventModeAutoStartDecision.Abandon, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, "")));
            Assert.AreEqual(EventModeAutoStartDecision.Abandon, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Blocked(PlaytestGateStatus.Unreachable, "dns")));
        }
    }
}
