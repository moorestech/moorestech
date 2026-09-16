using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 了解が1回だけ効き、その1回で待機が解けることを押さえる（ADR 0040 の言語選択ゲート・CrashReportGateと同じ契約）
    // Pins that exactly one acknowledgement takes effect and releases the wait (the same contract as the ADR 0040 language gate and CrashReportGate)
    public class PlaytestConsentGateTest
    {
        [SetUp]
        [TearDown]
        public void RemoveFlag()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
        }

        [Test]
        public void 既読フラグが無ければ待機し了解で解ける()
        {
            Assert.IsFalse(PlaytestConsentFlag.IsAcknowledged());
            var gate = new PlaytestConsentGate(!PlaytestConsentFlag.IsAcknowledged());
            Assert.IsTrue(gate.IsWaitingAcknowledgement);

            Assert.AreEqual(PlaytestConsentResult.Acknowledged, gate.Acknowledge());
            Assert.IsFalse(gate.IsWaitingAcknowledgement);
            Assert.IsTrue(gate.WaitForAcknowledgementAsync().Status.IsCompleted());
            Assert.IsTrue(PlaytestConsentFlag.IsAcknowledged());
        }

        [Test]
        public void 既読フラグがあれば待機しない()
        {
            PlaytestConsentFlag.Acknowledge();
            var gate = new PlaytestConsentGate(!PlaytestConsentFlag.IsAcknowledged());
            Assert.IsFalse(gate.IsWaitingAcknowledgement);
            Assert.IsTrue(gate.WaitForAcknowledgementAsync().Status.IsCompleted());
        }

        [Test]
        public void 二度目の了解は弾く()
        {
            var gate = new PlaytestConsentGate(true);
            Assert.AreEqual(PlaytestConsentResult.Acknowledged, gate.Acknowledge());
            Assert.AreEqual(PlaytestConsentResult.AlreadyAcknowledged, gate.Acknowledge());
        }

        // 待機しないゲートは初期状態から完了済み。CrashReportGateの同型テストと対になる観点
        // A non-waiting gate starts already completed; mirrors CrashReportGate's equivalent test
        [Test]
        public void 待機しないゲートは即座に完了する()
        {
            var gate = new PlaytestConsentGate(false);
            Assert.IsTrue(gate.WaitForAcknowledgementAsync().Status.IsCompleted());
        }
    }
}
