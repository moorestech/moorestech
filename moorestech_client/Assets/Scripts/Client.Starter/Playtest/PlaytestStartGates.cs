using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// プレイテストの開始ゲート。初回起動の同意表示と前回異常終了の確認を順に出し、応答があるまで開始を止める。
    /// The playtest start gates: shows the first-boot consent notice then the previous-crash confirmation in order, and holds the start until each is answered.
    /// </summary>
    public static class PlaytestStartGates
    {
        public static async UniTask WaitForPlaytestGatesAsync()
        {
            var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
            var artifacts = PreviousSessionSalvage.Artifacts ?? new PreviousSessionArtifacts { PreviousExitWasClean = true };

            // 画面を出せないなら止めない方を採る。前回分は last-session に残るので次回起動で聞き直せる
            // With no screen to show, not blocking wins: the salvage stays in last-session and the next boot can ask again
            if (hub == null)
            {
                if (!artifacts.PreviousExitWasClean) Debug.LogError("PlaytestStartGates: WebUiHostが起動しておらず前回異常終了の確認を出せないため、確認せずに開始します");
                return;
            }

            // 同意表示 → 前回異常終了の確認 の順。何が送られるかを読む前に送信可否を聞かない
            // Consent first, then the previous-crash confirmation; never ask to send before showing what gets sent
            var consentGate = PlaytestGateBinder.BindConsentGate(hub, PlaytestConsentFlag.IsAcknowledged());
            await consentGate.WaitForAcknowledgementAsync();

            // 登録は待機の有無に関わらず無条件。条件付き登録だとWeb側の購読が固着する
            // Registration happens unconditionally regardless of the wait; conditional registration would wedge the web-side subscription
            var gate = PlaytestGateBinder.BindCrashReportGate(hub, artifacts, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()));
            await gate.WaitForResponseAsync();

            // 応答の継続はaction処理スタックの中で走る。ここで手放さないと初期化の間WSの受信ループが止まる
            // The continuation resumes inside the action's stack, so yielding here keeps the WS receive loop alive during initialization
            await UniTask.Yield();
        }
    }
}
