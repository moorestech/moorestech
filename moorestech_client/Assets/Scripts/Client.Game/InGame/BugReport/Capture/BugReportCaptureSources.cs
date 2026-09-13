using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.Common;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Core.Update;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Capture
{
    // 確保セッションがゲームから記録を取る実装。テストではフェイクに差し替わる
    // The in-game implementation the capture session takes its records from; replaced by a fake in tests
    public sealed class BugReportCaptureSources : IBugReportCaptureSources
    {
        // スクリーンショットはUnityが次フレーム以降に書き出すため、ファイル出現をこの秒数まで待つ
        // Unity writes the screenshot on a later frame, so wait this many seconds for the file to appear
        private const float ScreenshotWaitSeconds = 10f;

        private const string StagingDirectoryName = "staging";
        private const string ScreenshotFileName = "screenshot.png";

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

        public UniTask<CapturedRecording> TakeRecordingAtCapture()
        {
            return _recorder.TakeRecordingAtCapture();
        }

        // 退避はファイルコピーなのでメインスレッドを塞がない。置き場は確保ごとの作業場なので送信中に次の確保が消さない
        // Staging is a file copy so it stays off the main thread; the per-capture workspace keeps the next capture from deleting a send in progress
        public UniTask<StagedServerCapture> StageServerCapture(string workDirectory, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            var stagingDirectory = Path.Combine(workDirectory, StagingDirectoryName);
            return UniTask.RunOnThreadPool(() => BugReportServerCaptureStaging.Stage(stagingDirectory, snapshotDirectory, snapshotFileNames, packetLogFileNames));
        }

        public IReadOnlyList<UnityLogEntry> Logs()
        {
            return _logRing.Dump();
        }

        public ClientStateSnapshot ClientState()
        {
            // カメラとプレイヤーはシーン都合で欠けうる。欠けたことは原点という実値ではなく有無で残す
            // Camera and player can be absent depending on the scene; absence is recorded as a flag, never as the origin passed off as a real value
            var camera = CameraManager.MainCamera?.Camera;
            if (camera == null) Debug.LogWarning("バグ報告: メインカメラが無いためカメラ位置を確保できません");
            var cameraPosition = camera == null ? Vector3.zero : camera.transform.position;
            var cameraEulerAngles = camera == null ? Vector3.zero : camera.transform.eulerAngles;

            var player = _playerSystemContainer.PlayerObjectController;
            if (player == null) Debug.LogWarning("バグ報告: プレイヤーが無いためプレイヤー位置を確保できません");
            var playerPosition = player == null ? Vector3.zero : player.Position;

            return new ClientStateSnapshot(cameraPosition, cameraEulerAngles, playerPosition, _currentUiState.ToString(), GameUpdater.CurrentTick, camera != null, player != null);
        }

        public async UniTask<string> CaptureScreenshot(string workDirectory)
        {
            Directory.CreateDirectory(workDirectory);
            // 確保ごとの作業場へ書く。マシン共通の置き場だと並行するPlayModeや再Escapeと掴み合う
            // Written into the per-capture workspace; a machine-wide directory would have parallel PlayModes and re-Escapes grab each other's file
            var path = Path.Combine(workDirectory, ScreenshotFileName);
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
