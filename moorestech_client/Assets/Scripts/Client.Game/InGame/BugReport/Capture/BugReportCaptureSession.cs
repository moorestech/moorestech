using System;
using System.Collections.Generic;
using System.IO;
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
        private bool _serverCapturePending;
        private bool _stagingPending;
        private bool _screenshotPending;

        // 同じ確保から2箱作ると同じ報告のdraft PRが2本出るので、送信の可否は専用の門が持つ
        // Two boxes from one capture raise two draft PRs for one report, so a dedicated gate owns whether a send may start
        private readonly BugReportSubmitGate _submitGate = new();

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
            _serverCapturePending = true;
            _stagingPending = false;
            _screenshotPending = true;
            _submitGate.Reset();

            // 録画境界・ログ・クライアント状態はサーバー要求と独立に即時確保する
            // Secure the recording cut, logs and client state immediately, independent of the server request
            var availability = _sources.GetRecordingAvailability();
            if (availability.IsAvailable)
            {
                _sources.CutRecordingSegment();
                data.VideoSegmentFiles = _sources.CompletedVideoSegments().ToList();
                data.FrameTicks = _sources.FrameTicks();
            }
            else
            {
                AddMissing(data, "video", availability.Reason);
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
        public void OnServerCaptureCompleted(long captureId, ulong tick, bool success, string snapshotDirectory, string serverDataDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            if (_data == null || captureId == 0 || _data.CaptureId != captureId)
            {
                Debug.Log($"バグ報告: 要求IDが一致しない完了イベントを無視します captureId:{captureId}");
                return;
            }
            if (!_serverCapturePending)
            {
                Debug.Log($"バグ報告: 既に待ちを打ち切った要求の完了イベントを無視します captureId:{captureId}");
                return;
            }

            _serverCapturePending = false;
            if (!success)
            {
                AddMissing(_data, "serverSnapshot", $"サーバーがスナップショットの書き出しに失敗した captureId:{captureId}");
                PublishStatus();
                return;
            }

            _data.ReportTick = tick;
            _data.ServerDataDirectory = serverDataDirectory;

            // ワールド定義は剪定されないので置き場のまま持つ。剪定されるスナップショットと区間だけを実体退避する
            // The world definition is never pruned so its root is kept as is; only the prunable snapshots and segments are staged
            _data.WorldRootDirectory = string.IsNullOrEmpty(snapshotDirectory) ? null : Path.GetDirectoryName(snapshotDirectory);
            _stagingPending = true;
            StageServerCapture(_data, snapshotDirectory, snapshotFileNames, packetLogFileNames, _beginCount).Forget();
            PublishStatus();
        }

        // 送信してよいかを判定し、許可なら記録一式を渡して送信中にする。判定の権威はここ1箇所
        // Decides whether a send may start and hands over the records; this is the single authority for that decision
        public BugReportSubmitTicket TryBeginSubmit()
        {
            return _submitGate.TryBegin(_data, _status.Value.CapturePending);
        }

        // 書き出しの結果を確保状態へ戻す。欠損は書き出し側が確定させるので、ここで丸ごと置き換える
        // Feeds the write result back into the capture state; the writer settles the missing list, so it is replaced wholesale
        public void CompleteSubmit(BugReportCapturedData data, bool ready, IReadOnlyList<MissingItem> missing)
        {
            if (_data != data)
            {
                Debug.LogWarning("バグ報告: 別の確保に差し替わった後の送信結果なので確保状態へ戻しません");
                return;
            }

            _submitGate.Complete(ready);
            _data.Missing.Clear();
            _data.Missing.AddRange(missing);
            PublishStatus();
        }

        private async UniTaskVoid RequestServerCapture(BugReportCapturedData data, int beginCount)
        {
            var request = await _sources.RequestServerCapture();
            if (beginCount != _beginCount) return;

            // 受理されなかった要求には完了イベントが来ない。待たずに欠損として確定させる
            // A rejected request never gets a completion event, so finish it as missing instead of waiting
            if (!request.Accepted)
            {
                _serverCapturePending = false;
                AddMissing(data, "serverSnapshot", $"サーバーが即時スナップショット要求を受け付けなかった reason:{request.RejectedReason}");
                PublishStatus();
                return;
            }

            data.CaptureId = request.CaptureId;
            PublishStatus();

            await _sources.WaitServerCaptureTimeout();
            if (beginCount != _beginCount || !_serverCapturePending) return;

            _serverCapturePending = false;
            AddMissing(data, "serverSnapshot", $"完了イベントが {ServerCaptureTimeoutSeconds}s 以内に届かなかった captureId:{request.CaptureId}");
            PublishStatus();
        }

        // 記入中もサーバーの剪定は進むため、Escape時点の記録は名前ではなく実体で確保する（ADR 0057）
        // The server keeps pruning while the user types, so the Escape-moment records are secured as files, not names (ADR 0057)
        private async UniTaskVoid StageServerCapture(BugReportCapturedData data, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames, int beginCount)
        {
            var staged = await _sources.StageServerCapture(snapshotDirectory, snapshotFileNames, packetLogFileNames);
            if (beginCount != _beginCount) return;

            _stagingPending = false;
            data.StagedSnapshotDirectory = staged.StagingDirectory;
            data.SnapshotFileNames = staged.SnapshotFileNames.ToList();
            data.PacketLogFileNames = staged.PacketLogFileNames.ToList();
            foreach (var item in staged.Missing) AddMissing(data, item.Item, item.Reason);
            PublishStatus();
        }

        private async UniTaskVoid CaptureScreenshot(BugReportCapturedData data, int beginCount)
        {
            var path = await _sources.CaptureScreenshot();
            if (beginCount != _beginCount) return;

            _screenshotPending = false;
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
            var pending = _serverCapturePending || _stagingPending || _screenshotPending;
            _status.Value = new BugReportCaptureStatus(_data != null, pending, missing);
        }
    }
}
