using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Client.Common;
using Core.Update;
using Game.Paths;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.BugReport.Recording
{
    // 描画結果を10fpsで読み出し ffmpeg へ流す。区間は10秒単位のリングで直近RetentionSeconds秒を保持する
    // Reads the rendered frame at 10fps and streams it to ffmpeg; a ring of 10s segments keeps the last RetentionSeconds
    public sealed class GameFrameRecorder : IInitializable, ITickable, IDisposable
    {
        public const int Width = 1280;
        public const int Height = 720;
        public const int Fps = 10;
        public const int SegmentSeconds = 10;
        public const double RetentionSeconds = 120;

        // ffmpeg自身のsegment_wrapに渡す本数。CutSegmentが毎回再起動させるため通常はここまで貯まらない安全弁
        // Passed to ffmpeg's own segment_wrap; a safety margin that normally never fills since CutSegment restarts first
        public const int LiveSegmentWrapCount = 12;

        public const string MissingFfmpegReason = "ffmpeg が見つかりません（MOORESTECH_FFMPEG か PATH で指定）";
        public const string NoMainCameraReason = "録画リングのフレーム取り込みをスキップしました（メインカメラ未登録）";

        private const string LiveDirectoryName = "live";

        // 並列worktreeが同じマシン共通パスを取り合わないよう、このプロセス専用のサブディレクトリへ書く
        // Scoped to this process's own subdirectory so parallel worktrees never fight over the machine-wide path
        private static readonly string ProcessDirectory = Path.Combine(GameSystemPaths.BugReportRecordingDirectory, $"pid_{Process.GetCurrentProcess().Id}");

        private readonly string _directory = ProcessDirectory;
        private readonly string _liveDirectory = Path.Combine(ProcessDirectory, LiveDirectoryName);
        private string _ffmpegPath;
        private FfmpegProcess _ffmpeg;
        private RenderTexture _scaledTexture;
        private float _nextCaptureTime;
        private bool _readbackInFlight;

        // 止まった理由はlatchするが、可用性の判定は必ずAvailabilityが行う。理由だけを外へ出すと空理由の不可用が作れてしまう
        // The stop reason is latched, but only Availability decides usability; exposing the reason alone would allow an empty-reason unavailable
        private string _latchedUnavailableReason = "";

        public FrameTickLog TickLog { get; } = new();

        // 可用性と理由は同じ1箇所で組み立てる。録れていないのに理由が空、が外から観測できないようにする
        // Usability and its reason are assembled in one place so "not recording with an empty reason" is never observable
        public RecordingAvailability Availability =>
            _ffmpeg != null && _ffmpeg.IsRunning ? RecordingAvailability.Available() : RecordingAvailability.Unavailable(_latchedUnavailableReason);

        // ffmpegが無いときの縮退理由。無音で諦めず理由を残し、報告側が欠損として記録できるようにする
        // The degradation reason when ffmpeg is absent; never fail silently so the report can record the gap
        public static RecordingAvailability ResolveInitialAvailability(string ffmpegPath)
        {
            if (ffmpegPath != null) return RecordingAvailability.Available();
            Debug.LogWarning($"録画リングを開始しません: {MissingFfmpegReason}");
            return RecordingAvailability.Unavailable(MissingFfmpegReason);
        }

        private bool IsRecording => _ffmpeg != null && _ffmpeg.IsRunning;

        public void Initialize()
        {
            // テスト・プレイテスト経路はここで止める。既存のプレイテストDSL等とScreenCapture/Camera経路を奪い合わないため
            // Stops here on test/playtest boots; otherwise they contend with the playtest DSL etc. over the capture path
            if (!BugReportRecordingSettings.Enabled)
            {
                _latchedUnavailableReason = BugReportRecordingSettings.DisabledReason;
                return;
            }
            _ffmpegPath = FfmpegLocator.Find();
            _latchedUnavailableReason = ResolveInitialAvailability(_ffmpegPath).Reason;
            if (_ffmpegPath == null) return;
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            Directory.CreateDirectory(_liveDirectory);
            _scaledTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            StartProcess();
        }

        public void Tick()
        {
            if (!IsRecording || _readbackInFlight || Time.unscaledTime < _nextCaptureTime) return;
            _nextCaptureTime = Time.unscaledTime + 1f / Fps;

            // MainCameraへ直接Renderする。ScreenCaptureはEditorではEditorウィンドウを写しGame Viewを写さないため使わない
            // Renders straight from MainCamera; ScreenCapture captures the Editor window rather than Game View in-editor
            var gameCamera = CameraManager.MainCamera;
            if (gameCamera == null)
            {
                Debug.LogWarning(NoMainCameraReason);
                return;
            }
            var camera = gameCamera.Camera;
            var previousTarget = camera.targetTexture;
            camera.targetTexture = _scaledTexture;
            camera.Render();
            camera.targetTexture = previousTarget;

            _readbackInFlight = true;
            var tick = GameUpdater.CurrentTick;
            var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AsyncGPUReadback.Request(_scaledTexture, 0, TextureFormat.RGBA32, request => OnReadback(request, unixMs, tick));
        }

        // 現在の区間を確定して新しい区間から録り直す。確保時点より後のフレームを混ぜないため
        // Finalize the current segment and restart on a fresh one so frames after the capture moment stay out
        public void CutSegment()
        {
            var availability = Availability;
            if (!availability.IsAvailable)
            {
                Debug.LogWarning($"録画区間を確定できません（録画していません）: {availability.Reason}");
                return;
            }
            _ffmpeg.Stop();
            RecordingSegmentRing.PromoteCompletedSegments(_liveDirectory, _directory, RetentionSeconds);
            StartProcess();
        }

        // 更新時刻順（古い→新しい）。書き込み中の最新区間は含めない
        // Ordered oldest to newest by write time; excludes the segment currently being written
        public IReadOnlyList<string> CompletedSegmentFilesInOrder()
        {
            if (!Directory.Exists(_directory)) return Array.Empty<string>();
            return RecordingSegmentRing.ListInOrder(_directory, _liveDirectory, IsRecording);
        }

        public void Stop()
        {
            _ffmpeg?.Stop();
            _ffmpeg = null;
        }

        // PlayModeの出入り・コンテナ破棄で必ず1本の経路から呼ばれる（VContainerがSingleton IDisposableを破棄時にDisposeする）
        // Always reached through one path on PlayMode exit or container teardown (VContainer disposes IDisposable singletons)
        public void Dispose()
        {
            Stop();
        }

        private void StartProcess()
        {
            // Metal/D3D は読み出し行が上から、OpenGL系は下からなので後者だけ反転する
            // Metal/D3D read back rows top-down while OpenGL-style APIs read bottom-up, so flip only the latter
            var flip = !SystemInfo.graphicsUVStartsAtTop;
            _ffmpeg = FfmpegProcess.StartSegmentRecorder(_ffmpegPath, _liveDirectory, Width, Height, Fps, flip);
            if (_ffmpeg == null)
            {
                _latchedUnavailableReason = "ffmpeg の起動に失敗しました（ログ参照）";
                Debug.LogWarning($"録画リングを開始できません: {_latchedUnavailableReason}");
                return;
            }

            // 起動し直せた時点で前の理由は実態と合わない。残すと次の停止で古い理由が出る
            // Once the restart succeeds the old reason no longer matches reality; keeping it would surface a stale reason on the next stop
            _latchedUnavailableReason = "";
        }

        private void OnReadback(AsyncGPUReadbackRequest request, long unixMs, ulong tick)
        {
            _readbackInFlight = false;
            if (request.hasError)
            {
                Debug.LogWarning("録画フレームのGPU読み出しに失敗しました");
                return;
            }
            if (!IsRecording) return;
            var data = request.GetData<byte>();
            var frame = new byte[data.Length];
            data.CopyTo(frame);
            _ffmpeg.WriteFrame(frame);
            TickLog.Add(unixMs, tick);
        }
    }
}
