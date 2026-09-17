using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Playtest.Progress;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の報告送信。ポーズメニューの確保→送信actionと同じ順で箱を書き、受け口への到達印を待つ
    /// The smoke run's report send: writes a box in the same order as the pause-menu capture and submit action, then waits for the upload mark
    /// 前例は BugReportSubmitActionHandler（WebUIのクリック経路は配布ビルドで叩けないので、その本体を同じ順で呼ぶ）
    /// The precedent is BugReportSubmitActionHandler; the web UI click path cannot be driven in a player build, so its body is called in the same order
    /// </summary>
    public sealed class StandalonePlaytestSmokeReportSender
    {
        private const string SmokeDescription = "smoke";
        private const float CaptureTimeoutSeconds = 180f;
        private const float UploadTimeoutSeconds = 300f;

        private readonly BugReportCaptureSession _captureSession;
        private readonly BugReportBundleWriter _bundleWriter;
        private readonly IPlaytestProgressSink _progressSink;
        private readonly IPlaytestUploadRequester _uploadRequester;

        public StandalonePlaytestSmokeReportSender(BugReportCaptureSession captureSession, BugReportBundleWriter bundleWriter, IPlaytestProgressSink progressSink, IPlaytestUploadRequester uploadRequester)
        {
            _captureSession = captureSession;
            _bundleWriter = bundleWriter;
            _progressSink = progressSink;
            _uploadRequester = uploadRequester;
        }

        // 箱を書いて送信を要求する。成功なら箱の場所、失敗なら理由を返す
        // Writes the box and requests its upload; returns the box location on success, or the reason on failure
        public async UniTask<StandalonePlaytestSmokeStepOutcome> WriteAndRequestUploadAsync()
        {
            // ポーズメニューが開いたときと同じ確保を始め、確保の完了を期限付きで待つ
            // Begin the same capture the pause menu starts, then wait for it with a deadline
            _captureSession.BeginOnPauseMenu();
            var deadline = Time.realtimeSinceStartup + CaptureTimeoutSeconds;
            while (_captureSession.Status.Value.Kind == BugReportCaptureStatus.Capturing && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }

            // 確保中・確保なし・二重送信の判定は確保セッションが持つ。ここでは結論だけを理由に写す
            // The capture session owns the pending / no-session / double-send decision; only its verdict becomes the reason here
            var ticket = _captureSession.TryBeginSubmit();
            if (!ticket.Allowed) return StandalonePlaytestSmokeStepOutcome.Failed($"capture was not submittable within {CaptureTimeoutSeconds}s: {ticket.RefusedCode}");

            var written = await _bundleWriter.WriteAsync(ticket.Data, SmokeDescription, PlaytestReportKind.Bug);
            _captureSession.CompleteSubmit(ticket.Data, written.Ready, written.Missing);
            if (!written.Ready) return StandalonePlaytestSmokeStepOutcome.Failed($"the report box was not finished with READY: {written.BundleDirectory}");

            Debug.Log($"[PlaytestSmoke] report box written {written.BundleDirectory} missing:{written.Missing.Count}");
            _progressSink.RecordReportSent(PlaytestReportKind.Bug);
            _uploadRequester.RequestUpload();
            return StandalonePlaytestSmokeStepOutcome.Succeeded(written.BundleDirectory);
        }

        // 受け口へのアップロード完了は UPLOADED 印で観測する。送信を諦めた印が付いたら期限を待たず失敗にする
        // Upload completion is observed through the UPLOADED mark; a given-up mark fails at once without waiting for the deadline
        public async UniTask<StandalonePlaytestSmokeStepOutcome> WaitUploadedAsync(string bundleDirectory)
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
