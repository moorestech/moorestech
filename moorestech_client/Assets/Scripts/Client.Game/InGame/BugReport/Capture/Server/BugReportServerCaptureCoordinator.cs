using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Capture
{
    // サーバースナップショットの要求・完了・退避を1つの確保世代へ結び付ける
    // Binds server snapshot request, completion and staging to one capture generation
    public sealed class BugReportServerCaptureCoordinator
    {
        private readonly IBugReportCaptureSources _sources;
        private readonly BugReportCaptureProgress _progress;
        private readonly BugReportSubmitGate _submitGate;
        private BugReportCapturedData _data;
        private int _beginCount;

        public BugReportServerCaptureCoordinator(IBugReportCaptureSources sources, BugReportCaptureProgress progress, BugReportSubmitGate submitGate)
        {
            _sources = sources;
            _progress = progress;
            _submitGate = submitGate;
        }

        public void BeginCapture(BugReportCapturedData data, int beginCount)
        {
            _data = data;
            _beginCount = beginCount;
            RequestServerCapture(data, beginCount).Forget();
        }

        // サーバーの書き出し完了イベント。要求IDが一致するものだけを取り込む
        // The server's write-completed event; only the matching request id is taken in
        public void OnServerCaptureCompleted(ServerCaptureCompletion completion)
        {
            if (_data == null || completion.CaptureId == 0 || _data.CaptureId != completion.CaptureId)
            {
                Debug.Log($"バグ報告: 要求IDが一致しない完了イベントを無視します captureId:{completion.CaptureId}");
                return;
            }

            if (!_progress.IsServerCapturePending())
            {
                Debug.Log($"バグ報告: 既に待ちを打ち切った要求の完了イベントを無視します captureId:{completion.CaptureId}");
                return;
            }

            _progress.FinishServerCapture();
            if (!completion.Success)
            {
                AddMissing(_data, "serverSnapshot", $"サーバーがスナップショットの書き出しに失敗した captureId:{completion.CaptureId}");
                Publish();
                return;
            }

            // サーバー申告の縮退を残し、剪定対象の実体だけを作業場へ退避する
            // Preserve server-declared degradation and stage only the files subject to pruning
            if (!string.IsNullOrEmpty(completion.PacketLogDegradeReason)) AddMissing(_data, "packetLog", $"サーバーのパケット記録が縮退した reason:{completion.PacketLogDegradeReason} degradedAtTick:{completion.PacketLogDegradedAtTick}");
            _data.ReportTick = completion.Tick;
            _data.ServerDataDirectory = completion.ServerDataDirectory;
            _data.WorldRootDirectory = string.IsNullOrEmpty(completion.SnapshotDirectory) ? null : Path.GetDirectoryName(completion.SnapshotDirectory);
            _progress.BeginStaging();
            StageServerCapture(_data, completion, _beginCount).Forget();
            Publish();
        }

        private async UniTaskVoid RequestServerCapture(BugReportCapturedData data, int beginCount)
        {
            var request = await _sources.RequestServerCapture();
            if (beginCount != _beginCount) return;

            // 受理されなかった要求には完了イベントが来ないため、待たずに欠損へ確定する
            // A rejected request has no completion event, so settle it as missing immediately
            if (!request.Accepted)
            {
                _progress.FinishServerCapture();
                AddMissing(data, "serverSnapshot", $"サーバーが即時スナップショット要求を受け付けなかった reason:{request.RejectedReason}");
                Publish();
                return;
            }

            data.CaptureId = request.CaptureId;
            Publish();
            await _sources.WaitServerCaptureTimeout();
            if (beginCount != _beginCount || !_progress.IsServerCapturePending()) return;

            _progress.FinishServerCapture();
            AddMissing(data, "serverSnapshot", $"完了イベントが {BugReportCaptureSession.ServerCaptureTimeoutSeconds}s 以内に届かなかった captureId:{request.CaptureId}");
            Publish();
        }

        private async UniTaskVoid StageServerCapture(BugReportCapturedData data, ServerCaptureCompletion completion, int beginCount)
        {
            var staged = await _sources.StageServerCapture(data.CaptureWorkDirectory, completion.SnapshotDirectory, completion.SnapshotFileNames, completion.PacketLogFileNames);
            if (beginCount != _beginCount) return;

            _progress.FinishStaging();
            data.StagedSnapshotDirectory = staged.StagingDirectory;
            data.SnapshotFileNames = new System.Collections.Generic.List<string>(staged.SnapshotFileNames);
            data.PacketLogFileNames = new System.Collections.Generic.List<string>(staged.PacketLogFileNames);
            foreach (var item in staged.Missing) AddMissing(data, item.Item, item.Reason);
            Publish();
        }

        private void Publish()
        {
            _progress.Publish(_data, _submitGate);
        }

        private static void AddMissing(BugReportCapturedData data, string item, string reason)
        {
            Debug.LogWarning($"バグ報告の記録が欠けます item:{item} reason:{reason}");
            data.Missing.Add(new MissingItem { Item = item, Reason = reason });
        }
    }
}
