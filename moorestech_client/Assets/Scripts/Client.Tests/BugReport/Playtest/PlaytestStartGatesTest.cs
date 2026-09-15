using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter.Playtest;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Playtest;
using Client.WebUiHost.Game.Topics.Playtest;
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
        private bool _consentExisted;

        [SetUp]
        public void SetUp()
        {
            // 同意の既読フラグと迂回の印は本番と同じ場所にある。自分が作った分だけ後始末する
            // The consent flag and the bypass mark live in the production locations, so only what this test creates is cleaned up
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();
            PlaytestStartGateBypass.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestStartGateBypass.Clear();
            if (!_consentExisted && File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
        }

        // hubが無いと誰もゲートを描けない。ここで待つと起動が無音で永久停止するので、理由を残して素通しする
        // With no hub nobody can paint a gate, so waiting would stall the boot silently; it passes through with the reason logged
        [Test]
        public void hubが無ければ前回異常終了でも待たずに進む()
        {
            LogAssert.Expect(LogType.Error, "PlaytestStartGates: WebUiHostが起動しておらず前回異常終了の確認を出せないため、確認せずに開始します");

            var wait = PlaytestStartGates.WaitForGatesAsync(null, TestPreviousSessionArtifacts.Unclean());
            Assert.IsTrue(wait.Status.IsCompleted());
        }

        // 無人起動は応答者が居ない。ゲートは登録しつつ閉じた状態で出し、待たずに進む
        // An unattended boot has nobody to answer: the gates are registered already closed and the boot proceeds without waiting
        [Test]
        public void 無人起動の印があれば閉じたゲートを登録して待たない()
        {
            PlaytestStartGateBypass.Apply();
            var hub = new WebSocketHub();

            var wait = PlaytestStartGates.WaitForGatesAsync(hub, TestPreviousSessionArtifacts.Unclean());

            Assert.IsTrue(wait.Status.IsCompleted(), "無人起動なのに開始ゲートで待っている");
            Assert.IsNotNull(hub.ResolveTopic(CrashReportGateTopic.TopicName), "ゲートのtopicが未登録だとWeb側の購読が固着する");
            Assert.IsNotNull(hub.ResolveTopic(PlaytestConsentGateTopic.TopicName), "同意表示のtopicが未登録だとWeb側の購読が固着する");
        }

        // 同意を先に待つ。何が送られるかを読む前に送信可否を聞かないための順序で、逆順だと同意画面が後ろに隠れる
        // Consent is awaited first: the order exists so nobody is asked to send before reading what gets sent
        [Test]
        public void 同意の応答が済むまで前回異常終了の確認へ進まない()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var hub = new WebSocketHub();

            var wait = PlaytestStartGates.WaitForGatesAsync(hub, TestPreviousSessionArtifacts.Unclean());
            Assert.IsFalse(wait.Status.IsCompleted(), "同意表示で待っていない");

            // 同意だけ答えても、前回異常終了の確認が残っているので開始はまだ進まない
            // Answering only the consent leaves the crash confirmation outstanding, so the boot still does not proceed
            hub.ResolveAction("playtest.consent.acknowledge").ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.IsFalse(wait.Status.IsCompleted(), "同意だけで前回異常終了の確認を飛ばしている");

            // 2つ目に答えると両方の待機が解ける。最後の UniTask.Yield はEditModeでは進まないので待機解除の側を見る
            // Answering the second releases both waits; the trailing UniTask.Yield never advances in EditMode, so the released state is observed instead
            hub.ResolveAction("playtest.crash_report.respond").ExecuteAsync(new JObject { ["send"] = false }).GetAwaiter().GetResult();
            StringAssert.Contains("\"waiting\":false", hub.ResolveTopic(CrashReportGateTopic.TopicName).GetSnapshotJsonAsync().GetAwaiter().GetResult());
            StringAssert.Contains("\"waiting\":false", hub.ResolveTopic(PlaytestConsentGateTopic.TopicName).GetSnapshotJsonAsync().GetAwaiter().GetResult());
        }
    }
}
