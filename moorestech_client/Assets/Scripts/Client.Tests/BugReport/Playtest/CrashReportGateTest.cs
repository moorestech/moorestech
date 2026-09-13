using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 応答が1回だけ効き、その1回で待機が解けることを押さえる（ADR 0040 の言語選択ゲートと同じ契約）
    // Pins that exactly one answer takes effect and releases the wait (the same contract as the ADR 0040 language gate)
    public class CrashReportGateTest
    {
        private static CrashReportGate CreateWaitingGate(PreviousSessionArtifacts artifacts)
        {
            return new CrashReportGate(true, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()), artifacts);
        }

        [Test]
        public void 待機しないゲートは即座に完了する()
        {
            var gate = new CrashReportGate(false, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()), new PreviousSessionArtifacts());
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        [Test]
        public void 送らないを選ぶと箱を作らず待機が解ける()
        {
            var gate = CreateWaitingGate(new PreviousSessionArtifacts { PreviousExitWasClean = false });
            Assert.AreEqual(CrashReportResponseResult.Skipped, gate.Respond(false, ""));
            Assert.IsFalse(gate.IsWaitingSelection());
            Assert.IsNull(gate.LastWrittenBundleDirectory);
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        [Test]
        public void 送るを選ぶと箱ができ二度目の応答は弾かれる()
        {
            var gate = CreateWaitingGate(new PreviousSessionArtifacts { PreviousExitWasClean = false });
            Assert.AreEqual(CrashReportResponseResult.Sent, gate.Respond(true, "落ちた"));
            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.Respond(true, "二重"));
            Assert.IsNotNull(gate.LastWrittenBundleDirectory);
            Directory.Delete(gate.LastWrittenBundleDirectory, true);
        }

        // 待機しないゲートへの応答も「応答済み」で弾く。正常終了後に遅れて届いたクリックで箱が出来ないこと
        // An answer to a non-waiting gate is rejected too, so a late click after a clean exit cannot create a box
        [Test]
        public void 待機していないゲートへの応答は弾かれる()
        {
            var gate = new CrashReportGate(false, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()), new PreviousSessionArtifacts());
            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.Respond(true, "遅れて届いた"));
            Assert.IsNull(gate.LastWrittenBundleDirectory);
        }
    }
}
