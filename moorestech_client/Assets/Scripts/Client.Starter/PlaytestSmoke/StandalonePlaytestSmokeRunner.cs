using Client.PlaytestReceiver.Steam;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Submit;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の本体。smoke起動時だけコンテナへ登録される
    /// - phase1: 新規ワールド/チュートリアル/セーブ
    /// - phase2: ロード済みワールドで報告送信/アップロード確認
    /// The smoke run itself, registered in the container only on a smoke launch
    /// - phase1: fresh world, tutorial, save
    /// - phase2: report send and upload confirmation on the loaded world
    /// </summary>
    public sealed class StandalonePlaytestSmokeRunner : IStartable
    {
        private static readonly ServerSaveGenerationWaiter.RealtimeBudget SaveTimeout = new(180f);

        private readonly InitialHandshakeResponse _initialHandshakeResponse;
        private readonly ServerSaveGenerationWaiter _saveGenerationWaiter;
        private readonly StandalonePlaytestSmokeReportSender _reportSender;
        private readonly List<StandalonePlaytestSmokeStep> _steps = new();

        public StandalonePlaytestSmokeRunner(
            InitialHandshakeResponse initialHandshakeResponse,
            ServerSaveGenerationWaiter saveGenerationWaiter,
            BugReportCaptureSession captureSession,
            BugReportSubmitter submitter)
        {
            _initialHandshakeResponse = initialHandshakeResponse;
            _saveGenerationWaiter = saveGenerationWaiter;
            _reportSender = new StandalonePlaytestSmokeReportSender(captureSession, submitter);
        }

        public void Start()
        {
            var settings = StandalonePlaytestSmokeBootstrap.Settings;
            // コンテナ構築は開始ゲート通過後・初期化完了前。スキットやチュートリアルが揃ってから検証を始める
            // The container is built after the start gates but before initialization completes; verify once everything is up
            // Forgetが拾う例外はRunAsync内の未処理失敗(IO等)。無音で消さず開始前の失敗と同じ手続きで終了する
            // An exception Forget catches is an unhandled failure inside RunAsync (e.g. IO); it quits through the same procedure as a pre-run failure
            GameInitializedEvent.OnGameInitialized.Take(1).Subscribe(_ => RunAsync(settings).Forget(exception =>
                StandalonePlaytestSmokeBootstrap.Fail(settings, "runner", $"{exception.GetType()} {exception.Message}")));
        }

        private async UniTask RunAsync(StandalonePlaytestSmokeSettings settings)
        {
            // 初期化期限切れで既に失敗を書いて終了要求済みなら、遅れて来た初期化完了で結果を上書きしない
            // If the initialization deadline already wrote a failure and requested quit, a late initialization must not overwrite it
            if (!StandalonePlaytestSmokeBootstrap.IsActive)
            {
                Debug.LogError("[PlaytestSmoke] game initialized after the smoke run was abandoned; not running the steps");
                return;
            }

            var reportBundleDirectory = "";
            var reportSteamId = "";
            bool succeeded;

            switch (settings.Phase)
            {
                case StandalonePlaytestSmokePhase.PhaseOne:
                    succeeded = Record("tutorial-started", Time.realtimeSinceStartup, VerifyTutorialStarted())
                                && Record("save", Time.realtimeSinceStartup, await SaveAndWaitWrittenAsync());
                    break;
                case StandalonePlaytestSmokePhase.PhaseTwo:
                    succeeded = await SendReportAsync();
                    break;
                default:
                    succeeded = Record("phase", Time.realtimeSinceStartup, StandalonePlaytestSmokeStepOutcome.Failed($"unknown smoke phase {settings.Phase}"));
                    break;
            }

            var result = new StandalonePlaytestSmokeResult
            {
                phase = StandalonePlaytestSmokeSettings.ToArgument(settings.Phase),
                success = succeeded,
                message = FirstFailureMessage(),
                reportBundleDirectory = reportBundleDirectory,
                reportSteamId = reportSteamId,
                steps = _steps.ToArray(),
            };
            StandalonePlaytestSmokeResultWriter.Write(settings.ResultDirectory, result);
            Application.Quit(result.success ? 0 : 1);

            #region Internal

            StandalonePlaytestSmokeStepOutcome VerifyTutorialStarted()
            {
                // 現在チャレンジありならチュートリアル中
                // Tutorial is active when a current challenge exists
                var challengeCount = 0;
                foreach (var category in _initialHandshakeResponse.Challenges) challengeCount += category.CurrentChallenges.Count;
                return 0 < challengeCount
                    ? StandalonePlaytestSmokeStepOutcome.Succeeded("")
                    : StandalonePlaytestSmokeStepOutcome.Failed("the initial handshake carried no current challenge, so the tutorial has not started");
            }

            // セーブ要求の番号が書き出し完了通知に追いつくまで実時間の期限で待つ（終了時の待ちと同じ待ち手を共有する）
            // Waits with a real-time deadline until the save request's generation is reported written (sharing the shutdown wait's waiter)
            async UniTask<StandalonePlaytestSmokeStepOutcome> SaveAndWaitWrittenAsync()
            {
                var waitResult = await _saveGenerationWaiter.SaveAndWaitWrittenAsync(SaveTimeout);
                return waitResult switch
                {
                    ServerSaveGenerationWaiter.SaveWaitResult.Written => StandalonePlaytestSmokeStepOutcome.Succeeded(""),
                    ServerSaveGenerationWaiter.SaveWaitResult.NoResponse => StandalonePlaytestSmokeStepOutcome.Failed("the save request got no response within the packet timeout"),
                    _ => StandalonePlaytestSmokeStepOutcome.Failed($"the save was not reported written within {SaveTimeout.Seconds}s"),
                };
            }

            // 箱を書き、受け口のキー(SteamID)を控え、アップロード完了を待つ
            // Writes the box, notes the receiver key (SteamID), then waits for the upload
            async UniTask<bool> SendReportAsync()
            {
                var writeStartedAt = Time.realtimeSinceStartup;
                var written = await _reportSender.WriteAndRequestUploadAsync();
                if (!Record("report-written", writeStartedAt, written)) return false;
                reportBundleDirectory = written.Value;

                var steamIdOutcome = PlaytestLocalSteamIdReader.TryRead(out var steamId, out var steamIdFailure)
                    ? StandalonePlaytestSmokeStepOutcome.Succeeded(steamId)
                    : StandalonePlaytestSmokeStepOutcome.Failed(steamIdFailure);
                if (!Record("report-steam-id", Time.realtimeSinceStartup, steamIdOutcome)) return false;
                reportSteamId = steamIdOutcome.Value;

                return Record("report-uploaded", Time.realtimeSinceStartup, await _reportSender.WaitUploadedAsync(reportBundleDirectory));
            }

            string FirstFailureMessage()
            {
                foreach (var step in _steps)
                {
                    if (!step.success) return step.message;
                }
                return "";
            }

            #endregion
        }

        // 各ステップの所要時間と失敗理由を必ず残す（無音の失敗を作らない）。合否を返す
        // Always records each step's duration and failure reason so no failure is silent; returns whether it succeeded
        // 開始時刻は呼び出し側が await の前に読む。await 後に読むと所要時間が常に0になる
        // The caller reads the start time before awaiting; reading it afterwards would always record zero
        private bool Record(string name, float startedAt, StandalonePlaytestSmokeStepOutcome outcome)
        {
            _steps.Add(new StandalonePlaytestSmokeStep
            {
                name = name,
                success = outcome.Success,
                message = outcome.FailureReason,
                elapsedSeconds = Time.realtimeSinceStartup - startedAt,
            });
            if (!outcome.Success) Debug.LogError($"[PlaytestSmoke] step {name} failed: {outcome.FailureReason}");
            return outcome.Success;
        }
    }
}
