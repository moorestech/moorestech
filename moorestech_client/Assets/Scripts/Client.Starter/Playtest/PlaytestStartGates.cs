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
            var artifacts = PreviousSessionSalvage.ArtifactsOrNotRunDefault();

            // 画面を出せないなら止めない方を採る。退避物は last-session に残り、次回起動の退避が空でも読み戻して聞き直せる
            // With no screen to show, not blocking wins: the salvage stays in last-session and the next boot re-presents it even with an empty source
            if (hub == null)
            {
                if (!artifacts.PreviousExitWasClean) Debug.LogError("PlaytestStartGates: WebUiHostが起動しておらず前回異常終了の確認を出せないため、確認せずに開始します");
                return;
            }

            // 同意表示 → 前回異常終了の確認 の順。何が送られるかを読む前に送信可否を聞かない
            // Consent first, then the previous-crash confirmation; never ask to send before showing what gets sent
            var consentAcknowledged = PlaytestConsentFlag.IsAcknowledged();
            var consentGate = PlaytestGateBinder.BindConsentGate(hub, consentAcknowledged);
            await WaitWithStartLog(consentGate.WaitForAcknowledgementAsync(), "同意表示", !consentAcknowledged);

            // 登録は待機の有無に関わらず無条件。条件付き登録だとWeb側の購読が固着する
            // Registration happens unconditionally regardless of the wait; conditional registration would wedge the web-side subscription
            var gate = PlaytestGateBinder.BindCrashReportGate(hub, artifacts, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()));
            await WaitWithStartLog(gate.WaitForResponseAsync(), "前回異常終了の確認", !artifacts.PreviousExitWasClean);

            // 応答の継続はaction処理スタックの中で走る。ここで手放さないと初期化の間WSの受信ループが止まる
            // The continuation resumes inside the action's stack, so yielding here keeps the WS receive loop alive during initialization
            await UniTask.Yield();

            #region Internal

            // 待ちは上限を持たない。hubは生きているのにWebがゲートを描けないと無音で永久停止するため、待ち始めだけは必ず残す
            // The wait is unbounded, so a hub that lives while the web never paints the gate would stall silently; the start is always logged
            async UniTask WaitWithStartLog(UniTask wait, string gateName, bool blocks)
            {
                if (blocks) Debug.Log($"PlaytestStartGates: {gateName}の応答を待ちます（応答があるまでゲーム開始は進みません）");
                await wait;
            }

            #endregion
        }
    }
}
