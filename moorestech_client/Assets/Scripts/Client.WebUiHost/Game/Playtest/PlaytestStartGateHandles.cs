using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Game.Playtest
{
    /// <summary>
    /// 束ねた2ゲートの持ち手。待つ順序（同意 → 前回異常終了の確認）と待ち始めのログはここだけが持つ。
    /// Handles to the two bound gates; the wait order (consent, then the crash confirmation) and the wait-start logs live here alone.
    /// </summary>
    public sealed class PlaytestStartGateHandles
    {
        private readonly PlaytestConsentGate _consent;
        private readonly CrashReportGate _crashReport;

        internal PlaytestStartGateHandles(PlaytestConsentGate consent, CrashReportGate crashReport)
        {
            _consent = consent;
            _crashReport = crashReport;
        }

        // 何が送られるかを読む前に送信可否を聞かない。終了のキャンセルが来たら人の応答を待たずに抜ける
        // Never ask to send before showing what gets sent; an exit cancellation leaves without waiting for a human answer
        public async UniTask WaitInOrderAsync(CancellationToken ct)
        {
            await WaitWithStartLog(_consent.WaitForAcknowledgementAsync(), "同意表示", _consent.IsWaitingAcknowledgement);
            await WaitWithStartLog(_crashReport.WaitForResponseAsync(), "前回異常終了の確認", _crashReport.IsWaitingResponse);

            #region Internal

            // 待ちは上限を持たない。hubは生きているのにWebがゲートを描けないと無音で永久停止するため、待ち始めだけは必ず残す
            // The wait is unbounded, so a hub that lives while the web never paints the gate would stall silently; the start is always logged
            async UniTask WaitWithStartLog(UniTask wait, string gateName, bool blocks)
            {
                if (blocks) Debug.Log($"PlaytestStartGates: {gateName}の応答を待ちます（応答があるまでゲーム開始は進みません）");
                await wait.AttachExternalCancellation(ct);
            }

            #endregion
        }
    }
}
