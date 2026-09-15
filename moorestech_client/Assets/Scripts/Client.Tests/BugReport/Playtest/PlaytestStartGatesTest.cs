using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter.Playtest;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.StartGates;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport
{
    // 開始ゲートは「同意 → 前回異常終了の確認」の順で、画面を出せないときは止めない。順序も縮退もここで押さえる
    // The start gates run consent first, then the crash confirmation, and never block when no screen can be shown; both are pinned here
    public class PlaytestStartGatesTest
    {
        // CIはバッチモードで無人判定が常に立つ。対話起動の待ち方を検証するテストは理由なし（対話）を明示して渡す
        // CI runs in batch mode where the unattended check always fires, so attended-wait tests pass an explicit no-reason (attended) boot
        private const string AttendedBoot = null;

        private bool _consentExisted;

        [SetUp]
        public void SetUp()
        {
            // 同意の既読フラグは本番と同じ場所にある。自分が作った分だけ後始末する
            // The consent flag lives in the production location, so only what this test creates is cleaned up
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();

            // 迂回の印は読んだ時点で消費される。前のテストの残りをここで読み捨てる
            // The bypass mark is consumed on read, so any leftover from an earlier test is read away here
            PlaytestStartGateBypass.UnattendedReason();
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestStartGateBypass.UnattendedReason();
            if (!_consentExisted && File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
        }

        // hubが無いと誰もゲートを描けない。ここで待つと起動が無音で永久停止するので、理由を残して素通しする
        // With no hub nobody can paint a gate, so waiting would stall the boot silently; it passes through with the reason logged
        [Test]
        public void hubが無ければ前回異常終了でも待たずに進む()
        {
            LogAssert.Expect(LogType.Error, "PlaytestStartGates: WebUiHostが起動しておらず前回異常終了の確認を出せないため、確認せずに開始します");

            var wait = PlaytestStartGates.WaitForGatesAsync(null, TestPreviousSessionArtifacts.Unclean(), null, CancellationToken.None);
            Assert.IsTrue(wait.Status.IsCompleted());
        }

        // 無人起動は応答者が居ない。ゲートは登録しつつ閉じた状態で出し、待たずに進む
        // An unattended boot has nobody to answer: the gates are registered already closed and the boot proceeds without waiting
        [Test]
        public void 無人起動の印があれば閉じたゲートを登録して待たない()
        {
            PlaytestStartGateBypass.Apply();
            var hub = new WebSocketHub();

            var wait = PlaytestStartGates.WaitForGatesAsync(hub, TestPreviousSessionArtifacts.Unclean(), PlaytestStartGateBypass.UnattendedReason(), CancellationToken.None);

            Assert.IsTrue(wait.Status.IsCompleted(), "無人起動なのに開始ゲートで待っている");
            Assert.IsNotNull(hub.ResolveTopic(StartGateTopics.CrashReportName), "ゲートのtopicが未登録だとWeb側の購読が固着する");
            Assert.IsNotNull(hub.ResolveTopic(StartGateTopics.ConsentName), "同意表示のtopicが未登録だとWeb側の購読が固着する");
        }

        // 同意を先に待つ。何が送られるかを読む前に送信可否を聞かないための順序で、逆順だと同意画面が後ろに隠れる
        // Consent is awaited first: the order exists so nobody is asked to send before reading what gets sent
        [Test]
        public void 同意の応答が済むまで前回異常終了の確認へ進まない()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var hub = new WebSocketHub();

            var wait = PlaytestStartGates.WaitForGatesAsync(hub, TestPreviousSessionArtifacts.Unclean(), AttendedBoot, CancellationToken.None);
            Assert.IsFalse(wait.Status.IsCompleted(), "同意表示で待っていない");

            // 待機はWebへ配られている。配られなければゲートは描かれず、応答者の居ないまま止まる
            // The wait is published to the web; without it no gate is painted and the boot stalls with nobody to answer
            AssertWaiting(hub, StartGateTopics.ConsentName, true, StartGateTopics.ConsentPrecedence);
            AssertWaiting(hub, StartGateTopics.CrashReportName, true, StartGateTopics.CrashReportPrecedence);

            // 同意だけ答えても、前回異常終了の確認が残っているので開始はまだ進まない
            // Answering only the consent leaves the crash confirmation outstanding, so the boot still does not proceed
            var acknowledged = hub.ResolveAction("playtest.consent.acknowledge").ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.IsTrue(acknowledged.Ok, $"同意の了解が受理されていない error:{acknowledged.Error}");
            Assert.IsFalse(wait.Status.IsCompleted(), "同意だけで前回異常終了の確認を飛ばしている");

            // 2つ目に答えると両方の待機が解ける。最後の UniTask.Yield はEditModeでは進まないので待機解除の側を見る
            // Answering the second releases both waits; the trailing UniTask.Yield never advances in EditMode, so the released state is observed instead
            hub.ResolveAction("playtest.crash_report.respond").ExecuteAsync(new JObject { ["send"] = false }).GetAwaiter().GetResult();
            AssertWaiting(hub, StartGateTopics.CrashReportName, false, StartGateTopics.CrashReportPrecedence);
            AssertWaiting(hub, StartGateTopics.ConsentName, false, StartGateTopics.ConsentPrecedence);
        }

        // 既読の起動では同意を出さず、前回異常終了の確認だけで待つ
        // A boot with the consent already read skips the notice and waits only on the crash confirmation
        [Test]
        public void 同意が既読なら同意を出さず前回異常終了の確認だけを待つ()
        {
            PlaytestConsentFlag.Acknowledge();
            var hub = new WebSocketHub();

            var wait = PlaytestStartGates.WaitForGatesAsync(hub, TestPreviousSessionArtifacts.Unclean(), AttendedBoot, CancellationToken.None);

            AssertWaiting(hub, StartGateTopics.ConsentName, false, StartGateTopics.ConsentPrecedence);
            AssertWaiting(hub, StartGateTopics.CrashReportName, true, StartGateTopics.CrashReportPrecedence);
            Assert.IsFalse(wait.Status.IsCompleted(), "前回異常終了の確認で待っていない");

            // 既読なら了解は二度目扱い。成功に丸めると待機していないゲートへ答えたことが見えなくなる
            // With the flag read, an acknowledgement counts as a second one; folding it into success would hide an answer to a non-waiting gate
            var acknowledged = hub.ResolveAction("playtest.consent.acknowledge").ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.IsFalse(acknowledged.Ok);
            Assert.AreEqual("already_acknowledged", acknowledged.Error);
        }

        // 終了のキャンセルが来たら人の応答を待たずに抜ける。抜けないとPlay終了後も初期化の続きが残る
        // An exit cancellation leaves without waiting for a human answer; otherwise initialization lingers past play exit
        [Test]
        public void 終了のキャンセルで開始ゲートの待ちを打ち切る()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            using var exit = new CancellationTokenSource();

            var wait = PlaytestStartGates.WaitForGatesAsync(new WebSocketHub(), TestPreviousSessionArtifacts.Unclean(), AttendedBoot, exit.Token);
            exit.Cancel();

            Assert.IsTrue(wait.Status.IsCanceled());
        }

        private static void AssertWaiting(WebSocketHub hub, string topicName, bool waiting, int precedence)
        {
            var json = JObject.Parse(hub.ResolveTopic(topicName).GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.AreEqual(waiting, json["waiting"].Value<bool>(), $"{topicName} の waiting が期待と違う");
            Assert.AreEqual(precedence, json["precedence"].Value<int>(), $"{topicName} の precedence が期待と違う");
        }
    }
}
