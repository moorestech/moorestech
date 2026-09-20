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

        // 期限内は待ち、期限を過ぎたら断念する。無期限に待つと出展機がタイトルに居座った理由がどこにも残らない
        // It waits inside the deadline and abandons past it; waiting forever would leave no record of why the exhibition machine sat on the title
        [Test]
        public void 期限内は確定を待ち期限を過ぎたら自動開始を断念する()
        {
            Assert.AreEqual(EventModeAutoStartDecision.WaitForVerdict, EventModeAutoStart.DecideAutoStartWithinDeadline(PlaytestGateResult.Checking, 179f, 180f));
            Assert.AreEqual(EventModeAutoStartDecision.Abandon, EventModeAutoStart.DecideAutoStartWithinDeadline(PlaytestGateResult.Checking, 180f, 180f));
            Assert.AreEqual(EventModeAutoStartDecision.Abandon, EventModeAutoStart.DecideAutoStartWithinDeadline(PlaytestGateResult.NotEvaluated, 181f, 180f));
        }

        // 確定していれば経過時間に関わらず結論は変わらない
        // Once settled the conclusion no longer depends on how long it waited
        [Test]
        public void 確定済みの結論は期限の影響を受けない()
        {
            Assert.AreEqual(EventModeAutoStartDecision.Start, EventModeAutoStart.DecideAutoStartWithinDeadline(PlaytestGateResult.Allowed(null, "7656"), 999f, 180f));
            Assert.AreEqual(EventModeAutoStartDecision.Abandon, EventModeAutoStart.DecideAutoStartWithinDeadline(PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, ""), 0f, 180f));
        }

        [Test]
        public void 照合に止められたら自動開始を断念する()
        {
            Assert.AreEqual(EventModeAutoStartDecision.Abandon, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, "")));
            Assert.AreEqual(EventModeAutoStartDecision.Abandon, EventModeAutoStart.DecideAutoStart(PlaytestGateResult.Blocked(PlaytestGateStatus.Unreachable, "dns")));
        }
    }
}
