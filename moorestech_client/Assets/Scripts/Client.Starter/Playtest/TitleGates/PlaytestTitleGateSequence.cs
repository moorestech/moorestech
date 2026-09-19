using System.Threading;
using Client.Game.InGame.BugReport.Submit;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// タイトルのゲートを「同意 → 前回異常終了の確認」の順に進め、持ち越した記録の送信要求を同意の後ろへ置く（ADR 0065）。
    /// Advances the title gates in the order consent then previous-crash confirmation, and places the carried-over upload request behind the consent (ADR 0065).
    /// </summary>
    public sealed class PlaytestTitleGateSequence
    {
        private readonly ReactiveProperty<PlaytestTitleGateStep> _step;
        private readonly PlaytestConsentGate _consent;
        private readonly CrashReportGate _crashReport;
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly bool _uploadsEnabled;

        public IReadOnlyReactiveProperty<PlaytestTitleGateStep> Step => _step;

        // 段階の器は持ち主（PlaytestTitleGates）から受け取る。開始経路はそちらの静的窓口から同じ値を読む
        // The step holder comes from its owner (PlaytestTitleGates), whose static window the start paths read the same value from
        internal PlaytestTitleGateSequence(ReactiveProperty<PlaytestTitleGateStep> step, PlaytestConsentGate consent, CrashReportGate crashReport, IPlaytestUploadRequester uploadRequester, bool uploadsEnabled)
        {
            _step = step;
            _consent = consent;
            _crashReport = crashReport;
            _uploadRequester = uploadRequester;
            _uploadsEnabled = uploadsEnabled;
        }

        // 待たない段階は同期で抜けるので、既読かつ正常終了なら呼んだその場で Passed になる
        // A step that does not wait completes synchronously, so a read consent with a clean exit reaches Passed within this call
        internal async UniTask RunAsync(CancellationToken ct)
        {
            // 未読なら了解まで止める。持ち越しの送信は了解の後ろ（既読ならこの直後）に置く（ADR 0065）
            // Unread: hold until acknowledged. The carried-over upload sits after the acknowledgement (right here when already read) (ADR 0065)
            if (_consent.IsWaitingAcknowledgement)
            {
                EnterStep(PlaytestTitleGateStep.Consent);
                await _consent.WaitForAcknowledgementAsync().AttachExternalCancellation(ct);
            }
            RequestUploadIfEnabled("consent settled");

            // 何が送られるかを見せてから送信可否を聞く
            // Ask about sending only after showing what gets sent
            if (_crashReport.IsWaitingResponse)
            {
                EnterStep(PlaytestTitleGateStep.CrashReport);
                await _crashReport.WaitForResponseAsync().AttachExternalCancellation(ct);
            }
            EnterStep(PlaytestTitleGateStep.Passed);

            #region Internal

            // 待ちは上限を持たない。画面が出ないと無音で止まるため、段階の変化は必ずログに残す
            // The wait is unbounded; a missing screen would stall silently, so every step change is logged
            void EnterStep(PlaytestTitleGateStep step)
            {
                Debug.Log($"[PlaytestTitleGates] step {step}");
                _step.Value = step;
            }

            #endregion
        }

        public PlaytestConsentResult AcknowledgeConsent()
        {
            // 段階がConsentでない了解は拒否する。閉じたゲートへ届いた了解を「もう答えた」に畳むと段階違いという実際の理由が読めない
            // Reject an acknowledgement while the step is not Consent; folding it into "already acknowledged" would hide the real reason (wrong step)
            if (_step.Value != PlaytestTitleGateStep.Consent)
            {
                Debug.LogWarning($"[PlaytestTitleGates] AcknowledgeConsent refused: step is {_step.Value}, not Consent");
                return PlaytestConsentResult.NotAsked;
            }
            return _consent.Acknowledge();
        }

        public async UniTask<CrashReportResponseResult> RespondCrashReportAsync(bool send, string description)
        {
            // 段階がCrashReportでない応答は拒否する。同意待ち中の応答を通すと、了解前の送信要求と段階の飛び越しが起きる
            // Reject an answer while the step is not CrashReport; letting it through would request an upload before consent and skip a step
            if (_step.Value != PlaytestTitleGateStep.CrashReport)
            {
                Debug.LogWarning($"[PlaytestTitleGates] RespondCrashReportAsync refused: step is {_step.Value}, not CrashReport");
                return CrashReportResponseResult.NotAsked;
            }

            var result = await _crashReport.RespondAsync(send, description);

            // 箱を書いたら同じ窓口へ送信をもう一度要求する。照合直後の走行はもう終わっていることがある（ADR 0065）
            // After writing the box, request an upload again through the same port; the run started after the check may already be over (ADR 0065)
            if (result == CrashReportResponseResult.Sent) RequestUploadIfEnabled("crash box written");
            return result;
        }

        private void RequestUploadIfEnabled(string trigger)
        {
            if (!_uploadsEnabled)
            {
                Debug.Log($"[PlaytestTitleGates] upload not requested after {trigger}: uploads are not enabled for this boot (developer mode, or an unattended boot whose consent is unread)");
                return;
            }
            _uploadRequester.RequestUpload();
        }
    }
}
