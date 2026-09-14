using Client.PlaytestReceiver.Gate;
using Client.Starter;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestLaunchGateBlockTest
    {
        [TearDown]
        public void RestoreGate()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);
        }

        [Test]
        public void 止められている間はローカル開始でシーンを読み込まない()
        {
            PlaytestLaunchGate.SetCurrent(new PlaytestGateResult(PlaytestGateStatus.NotAllowed, ""));
            var before = SceneManager.GetActiveScene().name;

            LocalGameLauncher.StartLocalGame();

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
    }
}
