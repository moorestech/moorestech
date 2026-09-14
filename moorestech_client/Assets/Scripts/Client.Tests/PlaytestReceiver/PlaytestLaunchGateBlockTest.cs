using Client.PlaytestReceiver.Gate;
using Client.Starter;
using NUnit.Framework;
using Server.Boot;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestLaunchGateBlockTest
    {
        [TearDown]
        public void RestoreGate()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);

            // 関所が壊れたときに常時記録を有効へ倒したまま次のテストへ渡さない
            // A broken guard must not hand an enabled always-on capture over to the next test
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
        }

        [Test]
        public void 止められている間はローカル開始でシーンを読み込まない()
        {
            PlaytestLaunchGate.SetCurrent(new PlaytestGateResult(PlaytestGateStatus.NotAllowed, ""));
            var before = SceneManager.GetActiveScene().name;

            LocalGameLauncher.StartLocalGame();

            // ガード直後の最初の副作用が常時記録の有効化なので、そこで早期returnを捕まえる
            // The first side effect after the guard is enabling always-on capture, so that pins the early return
            Assert.IsFalse(AlwaysOnCaptureSetting.Current.IsEnabled, "関所を通り抜けて常時記録が有効になっている");
            Assert.AreEqual(before, SceneManager.GetActiveScene().name);
        }

        [Test]
        public void 開発者モードでは関所が拒否しない()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);
            Assert.IsFalse(PlaytestLaunchGate.RejectStart("test"));
        }

        [Test]
        public void 止められているときRejectStartはtrueを返す()
        {
            PlaytestLaunchGate.SetCurrent(new PlaytestGateResult(PlaytestGateStatus.Unreachable, "dns"));
            Assert.IsTrue(PlaytestLaunchGate.RejectStart("test"));
        }

        [Test]
        public void 照合中もRejectStartはtrueを返す()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.Checking);
            Assert.IsTrue(PlaytestLaunchGate.RejectStart("test"));
        }
    }
}
