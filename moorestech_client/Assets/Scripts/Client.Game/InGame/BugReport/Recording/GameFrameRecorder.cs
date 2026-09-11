using System;
using System.Collections.Generic;
using System.IO;
using Core.Update;
using Game.Paths;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.Recording
{
    // 描画結果を10fpsで読み出し ffmpeg へ流す。区間は10秒×12本のリングで直近2分を保持する
    // Reads the rendered frame at 10fps and streams it to ffmpeg; a ring of 12×10s segments keeps the last two minutes
    public sealed class GameFrameRecorder : IInitializable, ITickable
    {
        public const int Width = 1280;
        public const int Height = 720;
        public const int Fps = 10;
        public const int SegmentSeconds = 10;
        public const int SegmentCount = 12;

        public const string MissingFfmpegReason = "ffmpeg が見つかりません（MOORESTECH_FFMPEG か PATH で指定）";

        private const string LiveDirectoryName = "live";

        private readonly string _directory = GameSystemPaths.BugReportRecordingDirectory;
        private readonly string _liveDirectory = Path.Combine(GameSystemPaths.BugReportRecordingDirectory, LiveDirectoryName);
        private string _ffmpegPath;
        private FfmpegProcess _ffmpeg;
        private RenderTexture _screenTexture;
        private RenderTexture _scaledTexture;
        private float _nextCaptureTime;
        private bool _readbackInFlight;

        public FrameTickLog TickLog { get; } = new();
        public bool IsRecording => _ffmpeg != null && _ffmpeg.IsRunning;
        public string UnavailableReason { get; private set; } = "";

        // ffmpegが無いときの縮退理由。無音で諦めず理由を残し、報告側が欠損として記録できるようにする
        // The degradation reason when ffmpeg is absent; never fail silently so the report can record the gap
        public static string ResolveUnavailableReason(string ffmpegPath)
        {
            if (ffmpegPath != null) return "";
            Debug.LogWarning($"録画リングを開始しません: {MissingFfmpegReason}");
            return MissingFfmpegReason;
        }

        public void Initialize()
        {
            _ffmpegPath = FfmpegLocator.Find();
            UnavailableReason = ResolveUnavailableReason(_ffmpegPath);
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

            EnsureScreenTexture();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_screenTexture);
            Graphics.Blit(_screenTexture, _scaledTexture);
            _readbackInFlight = true;
            var tick = GameUpdater.CurrentTick;
            var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AsyncGPUReadback.Request(_scaledTexture, 0, TextureFormat.RGBA32, request => OnReadback(request, unixMs, tick));
        }

        // 現在の区間を確定して新しい区間から録り直す。確保時点より後のフレームを混ぜないため
        // Finalize the current segment and restart on a fresh one so frames after the capture moment stay out
        public void CutSegment()
        {
            if (!IsRecording)
            {
                Debug.LogWarning($"録画区間を確定できません（録画していません）: {UnavailableReason}");
                return;
            }
            _ffmpeg.Stop();
            RecordingSegmentRing.PromoteCompletedSegments(_liveDirectory, _directory, SegmentCount);
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

        private void StartProcess()
        {
            // Metal/D3D は読み出し行が上から、OpenGL系は下からなので後者だけ反転する
            // Metal/D3D read back rows top-down while OpenGL-style APIs read bottom-up, so flip only the latter
            var flip = !SystemInfo.graphicsUVStartsAtTop;
            _ffmpeg = FfmpegProcess.StartSegmentRecorder(_ffmpegPath, _liveDirectory, Width, Height, Fps, flip);
            if (_ffmpeg == null)
            {
                UnavailableReason = "ffmpeg の起動に失敗しました（ログ参照）";
                Debug.LogWarning($"録画リングを開始できません: {UnavailableReason}");
            }
        }

        private void EnsureScreenTexture()
        {
            if (_screenTexture != null && _screenTexture.width == Screen.width && _screenTexture.height == Screen.height) return;
            if (_screenTexture != null) _screenTexture.Release();
            _screenTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
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
