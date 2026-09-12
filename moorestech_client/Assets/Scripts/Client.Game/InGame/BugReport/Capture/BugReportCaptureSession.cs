using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Capture
{
    // Escapeの瞬間の記録を確保し、サーバー側スナップショットの完了を待つ。次のEscapeで前回分は捨てる
    // Secures the Escape-moment records and waits for the server snapshot; the next Escape discards the previous set
    public sealed class BugReportCaptureSession
    {
        public const float ServerCaptureTimeoutSeconds = 15f;

        private readonly IBugReportCaptureSources _sources;
        private readonly ReactiveProperty<BugReportCaptureStatus> _status = new(new BugReportCaptureStatus(false, false, Array.Empty<string>()));

        private BugReportCapturedData _data;
        private bool _capturePending;

        // 何回目の確保かを持ち、非同期の続きが古い確保のものかを判定する
        // Counts begins so an async continuation can tell whether it belongs to a stale capture
        private int _beginCount;

        public IReadOnlyReactiveProperty<BugReportCaptureStatus> Status => _status;

        public BugReportCaptureSession(IBugReportCaptureSources sources)
        {
            _sources = sources;
        }

        // ポーズメニューを開いた瞬間に呼ばれ、その時点の記録一式を確保する
        // Called the moment the pause menu opens; secures every record as of that instant
        public void BeginOnPauseMenu()
        {
            _beginCount++;
            var data = new BugReportCapturedData();
            _data = data;
            _capturePending = false;

            // 録画境界・ログ・クライアント状態はサーバー要求と独立に即時確保する
            // Secure the recording cut, logs and client state immediately, independent of the server request
            var unavailableReason = _sources.RecordingUnavailableReason();
            if (unavailableReason.Length == 0)
            {
                _sources.CutRecordingSegment();
                data.VideoSegmentFiles = _sources.CompletedVideoSegments().ToList();
                data.FrameTicks = _sources.FrameTicks();
            }
            else
            {
                AddMissing(data, "video", unavailableReason);
            }

            data.Logs = _sources.Logs();
            data.ClientState = _sources.ClientState();
            data.ReportTick = data.ClientState.Tick;

            RequestServerCapture(data, _beginCount).Forget();
            CaptureScreenshot(data, _beginCount).Forget();
            PublishStatus();
        }

        // サーバーの書き出し完了イベント。要求IDが一致するものだけを取り込む
        // The server's write-completed event; only the matching request id is taken in
        public void OnServerCaptureCompleted(long captureId, ulong tick, bool success, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            if (_data == null || captureId == 0 || _data.CaptureId != captureId)
            {
                Debug.Log($"バグ報告: 要求IDが一致しない完了イベントを無視します captureId:{captureId}");
                return;
            }
            if (!_capturePending)
            {
                Debug.Log($"バグ報告: 既に待ちを打ち切った要求の完了イベントを無視します captureId:{captureId}");
                return;
            }

            _capturePending = false;
            if (!success)
            {
                AddMissing(_data, "serverSnapshot", $"サーバーがスナップショットの書き出しに失敗した captureId:{captureId}");
                PublishStatus();
                return;
            }

            _data.ReportTick = tick;
            _data.SnapshotDirectory = snapshotDirectory;
            _data.SnapshotFileNames = snapshotFileNames.ToList();
            _data.PacketLogFileNames = packetLogFileNames.ToList();
            PublishStatus();
        }

        // 送信時に確保済みの記録を取り出す。確保を始めていなければ null
        // Hands the secured records to the sender; null when no capture has been started
        public BugReportCapturedData TakeCapturedData()
        {
            return _data;
        }

        private async UniTaskVoid RequestServerCapture(BugReportCapturedData data, int beginCount)
        {
            _capturePending = true;
            var request = await _sources.RequestServerCapture();
            if (beginCount != _beginCount) return;

            // 受理されなかった要求には完了イベントが来ない。待たずに欠損として確定させる
            // A rejected request never gets a completion event, so finish it as missing instead of waiting
            if (!request.Accepted)
            {
                _capturePending = false;
                AddMissing(data, "serverSnapshot", $"サーバーが即時スナップショット要求を受け付けなかった reason:{request.RejectedReason}");
                PublishStatus();
                return;
            }

            data.CaptureId = request.CaptureId;
            PublishStatus();

            await _sources.WaitServerCaptureTimeout();
            if (beginCount != _beginCount || !_capturePending) return;

            _capturePending = false;
            AddMissing(data, "serverSnapshot", $"完了イベントが {ServerCaptureTimeoutSeconds}s 以内に届かなかった captureId:{request.CaptureId}");
            PublishStatus();
        }

        private async UniTaskVoid CaptureScreenshot(BugReportCapturedData data, int beginCount)
        {
            var path = await _sources.CaptureScreenshot();
            if (beginCount != _beginCount) return;

            if (path == null) AddMissing(data, "screenshot", "スクリーンショットの書き出しに失敗した");
            data.ScreenshotPath = path;
            PublishStatus();
        }

        private static void AddMissing(BugReportCapturedData data, string item, string reason)
        {
            Debug.LogWarning($"バグ報告の記録が欠けます item:{item} reason:{reason}");
            data.Missing.Add(new MissingItem { Item = item, Reason = reason });
        }

        private void PublishStatus()
        {
            var missing = _data == null ? new List<string>() : _data.Missing.Select(missingItem => missingItem.Item).ToList();
            _status.Value = new BugReportCaptureStatus(_data != null, _capturePending, missing);
        }
    }
}
