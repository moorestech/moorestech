using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.Context;
using Client.Game.InGame.Playtest.Progress;
using Client.Network.API;
using Client.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の本体。phase1=新規ワールド/チュートリアル/セーブ、phase2=ロード済みワールドで報告送信/アップロード確認
    /// The smoke run itself: phase1 is fresh world, tutorial and save; phase2 is report and upload confirmation on the loaded world
    /// </summary>
    public sealed class StandalonePlaytestSmokeRunner : IStartable
    {
        private const float SaveTimeoutSeconds = 180f;

        private readonly InitialHandshakeResponse _initialHandshakeResponse;
        private readonly StandalonePlaytestSmokeReportSender _reportSender;
        private readonly List<StandalonePlaytestSmokeStep> _steps = new();
        private long _completedSaveGeneration;

        public StandalonePlaytestSmokeRunner(
            InitialHandshakeResponse initialHandshakeResponse,
            BugReportCaptureSession captureSession,
            BugReportBundleWriter bundleWriter,
            IPlaytestProgressSink progressSink,
            IPlaytestUploadRequester uploadRequester)
        {
            _initialHandshakeResponse = initialHandshakeResponse;
            _reportSender = new StandalonePlaytestSmokeReportSender(captureSession, bundleWriter, progressSink, uploadRequester);
        }

        public void Start()
        {
            if (!StandalonePlaytestSmokeBootstrap.IsActive) return;

            // コンテナ構築は開始ゲート通過後・初期化完了前。スキットやチュートリアルが揃ってから検証を始める
            // The container is built after the start gates but before initialization completes; verify once everything is up
            GameInitializedEvent.OnGameInitialized.Take(1).Subscribe(_ => RunAsync(StandalonePlaytestSmokeBootstrap.Settings).Forget());
        }

        private async UniTask RunAsync(StandalonePlaytestSmokeSettings settings)
        {
            var reportBundleDirectory = "";
            var failure = "";

            if (settings.Phase == StandalonePlaytestSmokeSettings.PhaseOne)
            {
                failure = Record("tutorial-started", Time.realtimeSinceStartup, VerifyTutorialStarted());
                if (failure.Length == 0) failure = Record("save", Time.realtimeSinceStartup, await SaveAndWaitWrittenAsync());
            }
            else
            {
                var writeStartedAt = Time.realtimeSinceStartup;
                var written = await _reportSender.WriteAndRequestUploadAsync();
                failure = Record("report-written", writeStartedAt, written);
                reportBundleDirectory = written.Value;
                if (failure.Length == 0) failure = Record("report-uploaded", Time.realtimeSinceStartup, await _reportSender.WaitUploadedAsync(reportBundleDirectory));
            }

            var result = new StandalonePlaytestSmokeResult
            {
                phase = settings.Phase,
                success = failure.Length == 0,
                message = failure,
                reportBundleDirectory = reportBundleDirectory,
                steps = _steps.ToArray(),
            };
            StandalonePlaytestSmokeResultWriter.Write(settings.ResultDirectory, result);
            Application.Quit(result.success ? 0 : 1);
        }

        // 各ステップの所要時間と失敗理由を必ず残す（無音の失敗を作らない）。失敗理由を返し、成功なら空
        // Always records each step's duration and failure reason so no failure is silent; returns the reason, empty on success
        // 開始時刻は呼び出し側が await の前に読む。await 後に読むと所要時間が常に0になる
        // The caller reads the start time before awaiting; reading it afterwards would always record zero
        private string Record(string name, float startedAt, StandalonePlaytestSmokeStepOutcome outcome)
        {
            _steps.Add(new StandalonePlaytestSmokeStep
            {
                name = name,
                success = outcome.Success,
                message = outcome.FailureReason,
                elapsedSeconds = Time.realtimeSinceStartup - startedAt,
            });
            if (!outcome.Success) Debug.LogError($"[PlaytestSmoke] step {name} failed: {outcome.FailureReason}");
            return outcome.FailureReason;
        }

        private StandalonePlaytestSmokeStepOutcome VerifyTutorialStarted()
        {
            // 初期ハンドシェイクに現在チャレンジが乗っていればチュートリアルは開始している
            // The tutorial has started when the initial handshake carries at least one current challenge
            var challengeCount = 0;
            foreach (var category in _initialHandshakeResponse.Challenges) challengeCount += category.CurrentChallenges.Count;
            return 0 < challengeCount
                ? StandalonePlaytestSmokeStepOutcome.Succeeded("")
                : StandalonePlaytestSmokeStepOutcome.Failed("the initial handshake carried no current challenge, so the tutorial has not started");
        }

        // セーブ要求の番号が書き出し完了通知に追いつくまで待つ（前例: RemoteServerSaveFlushParticipant）
        // Waits until the save request's generation is reported written (precedent: RemoteServerSaveFlushParticipant)
        private async UniTask<StandalonePlaytestSmokeStepOutcome> SaveAndWaitWrittenAsync()
        {
            using var subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(WorldSaveCompletedEventPacket.EventTag, OnWorldSaveCompleted);
            // 応答待ちの期限は通信層が持ち、期限切れは null で返る
            // The response wait is bounded by the network layer, which returns null on timeout
            var response = await ClientContext.VanillaApi.Response.Save(default);
            if (response == null) return StandalonePlaytestSmokeStepOutcome.Failed("the save request got no response within the packet timeout");

            var deadline = Time.realtimeSinceStartup + SaveTimeoutSeconds;
            while (_completedSaveGeneration < response.RequestedSaveGeneration && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }
            return _completedSaveGeneration < response.RequestedSaveGeneration
                ? StandalonePlaytestSmokeStepOutcome.Failed($"save generation {response.RequestedSaveGeneration} was not reported written within {SaveTimeoutSeconds}s")
                : StandalonePlaytestSmokeStepOutcome.Succeeded("");
        }

        private void OnWorldSaveCompleted(byte[] payload)
        {
            var completed = MessagePackSerializer.Deserialize<WorldSaveCompletedEventPacket.WorldSaveCompletedMessagePack>(payload);
            if (_completedSaveGeneration < completed.CompletedSaveGeneration) _completedSaveGeneration = completed.CompletedSaveGeneration;
        }
    }
}
