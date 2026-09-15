using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Paths;
using Server.Boot;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.BugReport.Recording
{
    // 画面の最終出力を10fpsで読み出し ffmpeg へ流す。区間は10秒単位のリングで直近RetentionSeconds秒を保持する
    // Reads the composited screen at 10fps and streams it to ffmpeg; a ring of 10s segments keeps the last RetentionSeconds
    public sealed class GameFrameRecorder : IInitializable, ITickable, IDisposable
    {
        public const int SegmentSeconds = 10;
        public const double RetentionSeconds = 120;

        // ffmpeg自身のsegment_wrapに渡す本数。確保が毎回世代を切り替えるため通常はここまで貯まらない安全弁
        // Passed to ffmpeg's own segment_wrap; a safety margin that normally never fills since each capture switches generations
        public const int LiveSegmentWrapCount = 12;

        private const int Width = 1280;
        private const int Height = 720;
        private const int Fps = 10;
        // 読み出し中の1枚と書き込み待ちの1枚で足りる。空きが無いフレームは理由を残して落とす
        // One frame in readback and one queued for writing is enough; a frame with no free buffer is dropped with a reason
        private const int FrameBufferCount = 2;

        private const string LiveDirectoryPrefix = "live_";

        // 読み出し中フレームの落ち着きを待つ上限フレーム数。10fpsの1枚ぶんに数フレームの余裕を足した値
        // The frame budget for letting an in-flight readback settle: one 10fps frame plus a few frames of slack
        private const int ReadbackSettleFrameLimit = 10;

        // 並列worktreeと同じpidでの再生し直しが互いの録画へ書き足さないよう、このプロセスの今回のセッション専用ディレクトリへ書く
        // Scoped to this process's current-session directory so neither parallel worktrees nor a same-pid replay append to another's footage
        private readonly string _directory = ProcessSessionScope.CurrentSessionDirectory(GameSystemPaths.BugReportRecordingDirectory);

        // 世代の退避は連番を採るので直列化する。連続したEscapeが同じ番号を取り合わないため
        // Promotion assigns sequence numbers, so it is serialized; back-to-back Escapes must not race for one number
        private readonly object _promoteLock = new();
        private string _ffmpegPath;
        private FfmpegProcess _ffmpeg;
        private FrameBufferPool _framePool;
        private ScreenFrameReader _screenFrameReader;
        private string _liveDirectory;
        private int _segmentGeneration;
        private int _frameIndexInGeneration;
        private float _nextCaptureTime;
        private bool _readbackInFlight;

        // 止まった理由はlatchするが、可用性の判定は必ずAvailabilityが行う。理由だけを外へ出すと空理由の不可用が作れてしまう
        // The stop reason is latched, but only Availability decides usability; exposing the reason alone would allow an empty-reason unavailable
        private string _latchedUnavailableReason = "";

        public FrameTickLog TickLog { get; } = new();

        // 可用性と理由は同じ1箇所で組み立てる。録れていないのに理由が空、が外から観測できないようにする
        // Usability and its reason are assembled in one place so "not recording with an empty reason" is never observable
        private RecordingAvailability Availability =>
            _ffmpeg != null && _ffmpeg.IsRunning ? RecordingAvailability.Available() : RecordingAvailability.Unavailable(_latchedUnavailableReason);

        private bool IsRecording => _ffmpeg != null && _ffmpeg.IsRunning;
        public void Initialize()
        {
            // 常時記録を有効化していない起動経路はここで止める。可否の決定はAlwaysOnCaptureSettingが1つだけ持つ
            // Boot paths that never enabled always-on capture stop here; AlwaysOnCaptureSetting holds the only decision
            if (!AlwaysOnCaptureSetting.Current.IsEnabled)
            {
                _latchedUnavailableReason = AlwaysOnCaptureSetting.DisabledReason;
                Debug.Log($"録画リングを開始しません: {_latchedUnavailableReason}");
                return;
            }
            _ffmpegPath = FfmpegLocator.Find();
            _latchedUnavailableReason = FfmpegLocator.ResolveInitialAvailability(_ffmpegPath).Reason;
            if (_ffmpegPath == null) return;
            // 前回分の掃除は起動時の PreviousSessionSalvage がセッション単位で済ませている。書き先は常に新しいセッションなので消す物は無い
            // PreviousSessionSalvage already folded the previous sessions at boot; the target is always a fresh session, so nothing needs deleting
            Directory.CreateDirectory(_directory);
            _framePool = new FrameBufferPool(FrameBufferCount, Width * Height * 4);
            _screenFrameReader = new ScreenFrameReader(Width, Height);
            StartProcess();
        }

        public void Tick()
        {
            if (!IsRecording || _readbackInFlight || Time.unscaledTime < _nextCaptureTime) return;
            _nextCaptureTime = Time.unscaledTime + 1f / Fps;

            // 取るのはUI合成後の画面。カメラを描き直すとWeb UI(CEF)もUIも映らない
            // The source is the screen after UI composition; re-rendering the camera shows neither the Web UI (CEF) nor the UI
            var frameTexture = _screenFrameReader.CaptureScaledScreen();

            _readbackInFlight = true;
            var tick = GameUpdater.CurrentTick;
            var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AsyncGPUReadback.Request(frameTexture, 0, TextureFormat.RGBA32, request => OnReadback(request, unixMs, tick));
        }

        // Escape時点の記録境界をその場で確定し、エンコーダーの排出と終了待ちだけを後ろへ回す（最大10秒メインスレッドを塞がないため）
        // Settles the Escape-moment boundary inline and defers only the encoder drain and exit wait, which can block the main thread for 10s
        public async UniTask<CapturedRecording> TakeRecordingAtCapture()
        {
            var availability = Availability;
            if (!availability.IsAvailable)
            {
                Debug.LogWarning($"録画区間を確定できません（録画していません）: {availability.Reason}");
                return CapturedRecording.Unavailable(availability.Reason);
            }

            // 読み出し中の1枚はEscape以前のフレーム。切り替え前に落ち着かせないと確保区間の境界が読み出し1回ぶんずれる
            // The in-flight frame predates the Escape; without settling it first the captured boundary slips by one readback
            for (var i = 0; i < ReadbackSettleFrameLimit && _readbackInFlight; i++) await UniTask.Yield();
            if (_readbackInFlight) Debug.LogWarning("録画フレームの読み出し完了を待ち切れないまま世代を切り替えます（記録境界が1フレームずれます）");

            // 次の世代へ切り替える。これ以降のフレームは新しいliveへ入り、確保した区間へ混ざらない
            // Switch to the next generation; later frames land in the new live directory, never in the captured segments
            var finished = _ffmpeg;
            var finishedLiveDirectory = _liveDirectory;
            _segmentGeneration++;
            _frameIndexInGeneration = 0;
            StartProcess();
            var frameTicks = TickLog.Dump();

            await UniTask.RunOnThreadPool(() => DrainFinishedGeneration(finished, finishedLiveDirectory));
            return CapturedRecording.Available(RecordingSegmentRing.ListInOrder(_directory, _liveDirectory, IsRecording), frameTicks);
        }

        // PlayModeの出入り・コンテナ破棄で必ず1本の経路から呼ばれる（VContainerがSingleton IDisposableを破棄時にDisposeする）
        // Always reached through one path on PlayMode exit or container teardown (VContainer disposes IDisposable singletons)
        public void Dispose()
        {
            _ffmpeg?.Stop();
            _ffmpeg = null;
            _screenFrameReader?.Dispose();
        }

        private void StartProcess()
        {
            // Metal/D3D は読み出し行が上から、OpenGL系は下からなので後者だけ反転する
            // Metal/D3D read back rows top-down while OpenGL-style APIs read bottom-up, so flip only the latter
            var flip = !SystemInfo.graphicsUVStartsAtTop;
            _liveDirectory = Path.Combine(_directory, $"{LiveDirectoryPrefix}{_segmentGeneration:D4}");
            _ffmpeg = FfmpegProcess.StartSegmentRecorder(_ffmpegPath, _liveDirectory, Width, Height, Fps, flip, _framePool);
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

        private void DrainFinishedGeneration(FfmpegProcess finished, string liveDirectory)
        {
            finished.Stop();

            // 空になった世代のliveディレクトリは残るが、次回起動のPreviousSessionSalvageがこのセッションのディレクトリごと畳む
            // The emptied generation directory is left behind; the next boot's PreviousSessionSalvage folds this session's directory whole
            lock (_promoteLock)
            {
                RecordingSegmentRing.PromoteCompletedSegments(liveDirectory, _directory, RetentionSeconds);
            }
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

            // バッファは借りて書き終えたら返る。空きが無いのは書き込みが滞っているときなので理由を残して1枚落とす
            // Buffers are borrowed and returned once written; an empty pool means the writer lags, so one frame is dropped with a reason
            if (!_framePool.TryRent(out var frame))
            {
                Debug.LogWarning("録画フレームを破棄しました（空きバッファがありません）");
                return;
            }
            request.GetData<byte>().CopyTo(frame);

            // 受理されたフレームだけを対応表へ載せる。破棄フレームまで載せると動画とtickがずれる
            // Only accepted frames enter the tick log; logging dropped ones would desynchronize video and ticks
            if (!_ffmpeg.WriteFrame(frame)) return;
            TickLog.Add(unixMs, tick, _segmentGeneration, _frameIndexInGeneration);
            _frameIndexInGeneration++;
        }
    }
}
