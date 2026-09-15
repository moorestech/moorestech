using System.Threading;
using Client.Game.InGame.BugReport.LastSession;
using Client.WebUiHost.Boot;
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
        public static UniTask WaitForPlaytestGatesAsync(CancellationToken ct)
        {
            return WaitForGatesAsync(Client.WebUiHost.Boot.WebUiHost.Hub, PreviousSessionSalvage.RequireArtifacts(), ct);
        }

        // hubと退避結果を引数で受ける本体。順序とhub不在の縮退を、実プロセスを起こさずに検証できるようにするため分ける
        // The body takes the hub and the salvage result as arguments so the order and the hub-less degradation are verifiable without spawning a process
        internal static async UniTask WaitForGatesAsync(WebSocketHub hub, PreviousSessionArtifacts artifacts, CancellationToken ct)
        {
            // 画面を出せないなら止めない方を採る。退避物は last-session に残り、次回起動の退避が空でも読み戻して聞き直せる
            // With no screen to show, not blocking wins: the salvage stays in last-session and the next boot re-presents it even with an empty source
            if (hub == null)
            {
                if (!artifacts.PreviousExitWasClean) Debug.LogError("PlaytestStartGates: WebUiHostが起動しておらず前回異常終了の確認を出せないため、確認せずに開始します");
                return;
            }

            // 登録・無人判定・待つ順序はゲート側が持つ。ここはhub不在の縮退と、応答後に手放すことだけを受け持つ
            // Registration, the unattended decision and the order belong to the gates; this keeps only the hub-less degradation and the post-answer yield
            var gates = PlaytestGateBinder.BindForBoot(hub, artifacts);
            var waitsForAnswer = gates.IsAnyGateWaiting();
            await gates.WaitInOrderAsync(ct);

            // 応答の継続はaction処理スタックの中で走る。ここで手放さないと初期化の間WSの受信ループが止まる
            // The continuation resumes inside the action's stack, so yielding here keeps the WS receive loop alive during initialization
            // 待たなかった起動（無人・応答済み）は継続がaction内に無いので、手放さず同期で進める
            // A boot that never waited (unattended or already answered) has no continuation inside an action, so it proceeds synchronously
            if (waitsForAnswer) await UniTask.Yield();
        }
    }
}
