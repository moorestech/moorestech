using System.IO;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の報告送信（ポーズ確保と同順で送信）
    /// The smoke run's report send (same order as the pause-menu capture/submit)
    /// WebUIのクリック経路は配布ビルドで叩けないので、送信actionと共有する BugReportSubmitter を直接呼ぶ
    /// The web UI click path cannot be driven in a player build, so this calls the BugReportSubmitter the submit action shares
    /// </summary>
    internal sealed class StandalonePlaytestSmokeReportSender
    {
        private const string SmokeDescription = "smoke";
        private const float CaptureTimeoutSeconds = 180f;
        private const float UploadTimeoutSeconds = 300f;

        private readonly BugReportCaptureSession _captureSession;
        private readonly BugReportSubmitter _submitter;

        internal StandalonePlaytestSmokeReportSender(BugReportCaptureSession captureSession, BugReportSubmitter submitter)
        {
            _captureSession = captureSession;
            _submitter = submitter;
        }

        // 箱を書いて送信を要求する。成功なら箱の場所、失敗なら理由を返す
        // Writes the box and requests its upload; returns the box location on success, or the reason on failure
        internal async UniTask<StandalonePlaytestSmokeStepOutcome> WriteAndRequestUploadAsync()
        {
            // ポーズメニューが開いたときと同じ確保を始め、確保の完了を期限付きで待つ
            // Begin the same capture the pause menu starts, then wait for it with a deadline
            _captureSession.BeginOnPauseMenu();
            var deadline = Time.realtimeSinceStartup + CaptureTimeoutSeconds;
            while (_captureSession.Status.Value.Kind == BugReportCaptureStatus.Capturing && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }

            // 送信手続きは本番の送信actionと同じ1本を通す。検証機専用なので送信記録がプレイ進行へ残るのは許容する
            // The send goes through the same procedure as the real submit action; recording it in play progress is acceptable on the verifier
            var submitted = await _submitter.SubmitAsync(SmokeDescription, PlaytestReportKind.Bug);
            if (submitted.Submitted) return StandalonePlaytestSmokeStepOutcome.Succeeded(submitted.BundleDirectory);
            if (submitted.FailureCode == BugReportSubmitResult.BundleWriteFailed)
                return StandalonePlaytestSmokeStepOutcome.Failed($"the report box was not finished with READY: {submitted.BundleDirectory}");
            // 期限切れは確保セッションが確保中として拒否した場合だけ。NoCaptureSession等の即時拒否は別の理由として区別する
            // Only a capture-pending refusal from the session means timed out; an immediate refusal like NoCaptureSession is reported separately
            return submitted.FailureCode == BugReportSubmitTicket.CapturePending
                ? StandalonePlaytestSmokeStepOutcome.Failed($"capture was not submittable within {CaptureTimeoutSeconds}s: {submitted.FailureCode}")
                : StandalonePlaytestSmokeStepOutcome.Failed($"the capture session refused the submit: {submitted.FailureCode}");
        }

        // 受け口へのアップロード完了は UPLOADED 印で観測する。送信を諦めた印が付いたら期限を待たず失敗にする
        // Upload completion is observed through the UPLOADED mark; a given-up mark fails at once without waiting for the deadline
        internal async UniTask<StandalonePlaytestSmokeStepOutcome> WaitUploadedAsync(string bundleDirectory)
        {
            var uploadedPath = Path.Combine(bundleDirectory, PlaytestOutboxScanner.UploadedMarker);
            var failedPath = Path.Combine(bundleDirectory, PlaytestOutboxScanner.FailedMarker);
            var deadline = Time.realtimeSinceStartup + UploadTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (File.Exists(uploadedPath)) return StandalonePlaytestSmokeStepOutcome.Succeeded(bundleDirectory);
                if (File.Exists(failedPath)) return StandalonePlaytestSmokeStepOutcome.Failed($"the uploader gave up on the box: {failedPath}");
                await UniTask.Yield();
            }
            return StandalonePlaytestSmokeStepOutcome.Failed($"{PlaytestOutboxScanner.UploadedMarker} was not placed within {UploadTimeoutSeconds}s: {uploadedPath}");
        }
    }
}
