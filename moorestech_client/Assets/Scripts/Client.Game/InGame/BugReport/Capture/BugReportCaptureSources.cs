using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Client.Common;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.BugReport.Capture
{
    // 確保セッションがゲームから記録を取る実装。テストではフェイクに差し替わる
    // The in-game implementation the capture session takes its records from; replaced by a fake in tests
    public sealed class BugReportCaptureSources : IBugReportCaptureSources
    {
        // スクリーンショットはUnityが次フレーム以降に書き出すため、ファイル出現をこの秒数まで待つ
        // Unity writes the screenshot on a later frame, so wait this many seconds for the file to appear
        private const float ScreenshotWaitSeconds = 10f;

        private readonly GameFrameRecorder _recorder;
        private readonly UnityLogRing _logRing;
        private readonly PlayerSystemContainer _playerSystemContainer;

        // 画面はUIStateControlを見に行かず押し込んでもらう。見に行くと確保元→UI状態機械→ポーズ→確保元で生成が循環する
        // The screen is pushed in rather than read from UIStateControl; reading it would loop sources to the UI state machine and back
        private UIStateEnum _currentUiState = UIStateEnum.GameScreen;

        public BugReportCaptureSources(GameFrameRecorder recorder, UnityLogRing logRing, PlayerSystemContainer playerSystemContainer)
        {
            _recorder = recorder;
            _logRing = logRing;
            _playerSystemContainer = playerSystemContainer;
        }

        public void SetCurrentUiState(UIStateEnum uiState)
        {
            _currentUiState = uiState;
        }

        public async UniTask<BugReportServerCaptureRequest> RequestServerCapture()
        {
            var response = await ClientContext.VanillaApi.Response.RequestBugReportCapture(CancellationToken.None);
            if (response == null) return new BugReportServerCaptureRequest(false, 0, "サーバーから応答が返らなかった");
            return new BugReportServerCaptureRequest(response.Accepted, response.RequestedCaptureId, response.RejectedReason);
        }

        public UniTask WaitServerCaptureTimeout()
        {
            // ワールドは記入中も進むが、この待ちは通信待ちなので実時間で測る
            // The world keeps running while typing, but this wait is for the network and is measured in real time
            return UniTask.Delay(TimeSpan.FromSeconds(BugReportCaptureSession.ServerCaptureTimeoutSeconds), DelayType.Realtime);
        }

        public void CutRecordingSegment()
        {
            _recorder.CutSegment();
        }

        public IReadOnlyList<string> CompletedVideoSegments()
        {
            return _recorder.CompletedSegmentFilesInOrder();
        }

        public RecordingAvailability GetRecordingAvailability()
        {
            return _recorder.Availability;
        }

        // 退避はファイルコピーなのでメインスレッドを塞がない。置き場はプロセス毎に分け、並行するPlayModeと掴み合わない
        // Staging is a file copy so it stays off the main thread; the directory is per-process so parallel PlayModes never collide
        public UniTask<StagedServerCapture> StageServerCapture(string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            var stagingDirectory = Path.Combine(GameSystemPaths.BugReportDirectory, $"staging_pid_{Process.GetCurrentProcess().Id}");
            return UniTask.RunOnThreadPool(() => BugReportServerCaptureStaging.Stage(stagingDirectory, snapshotDirectory, snapshotFileNames, packetLogFileNames));
        }

        public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks()
        {
            return _recorder.TickLog.Dump();
        }

        public IReadOnlyList<UnityLogEntry> Logs()
        {
            return _logRing.Dump();
        }

        public ClientStateSnapshot ClientState()
        {
            // カメラとプレイヤーはシーン都合で欠けうるため、欠けたら原点として理由を残す
            // Camera and player can be absent depending on the scene; fall back to the origin and log why
            var camera = CameraManager.MainCamera?.Camera;
            if (camera == null) Debug.LogWarning("バグ報告: メインカメラが無いためカメラ位置を原点として記録します");
            var cameraPosition = camera == null ? Vector3.zero : camera.transform.position;
            var cameraEulerAngles = camera == null ? Vector3.zero : camera.transform.eulerAngles;

            var player = _playerSystemContainer.PlayerObjectController;
            if (player == null) Debug.LogWarning("バグ報告: プレイヤーが無いためプレイヤー位置を原点として記録します");
            var playerPosition = player == null ? Vector3.zero : player.Position;

            return new ClientStateSnapshot(cameraPosition, cameraEulerAngles, playerPosition, _currentUiState.ToString(), GameUpdater.CurrentTick);
        }

        public async UniTask<string> CaptureScreenshot()
        {
            Directory.CreateDirectory(GameSystemPaths.BugReportDirectory);
            // 置き場はマシン共通なので、確保ごとに別名にして並行するPlayModeや再Escapeと掴み合わない
            // The directory is machine-wide, so a per-capture name keeps parallel PlayModes and re-Escapes from grabbing each other's file
            var path = Path.Combine(GameSystemPaths.BugReportDirectory, $"screenshot_{Guid.NewGuid():N}.png");
            ScreenCapture.CaptureScreenshot(path);

            var startTime = Time.realtimeSinceStartup;
            while (!File.Exists(path))
            {
                if (ScreenshotWaitSeconds < Time.realtimeSinceStartup - startTime)
                {
                    Debug.LogWarning($"バグ報告: スクリーンショットが {ScreenshotWaitSeconds}s 以内に書き出されませんでした path:{path}");
                    return null;
                }
                await UniTask.Yield();
            }
            return path;
        }
    }
}
