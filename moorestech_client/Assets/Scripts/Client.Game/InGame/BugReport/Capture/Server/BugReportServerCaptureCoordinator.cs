using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Capture
{
    // 要求・完了・退避を1確保世代に紐付け
    // Binds request, completion, staging to one capture generation
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
            RequestServerCapture().Forget();

            #region Internal

            async UniTaskVoid RequestServerCapture()
            {
                var request = await _sources.RequestServerCapture();
                if (beginCount != _beginCount) return;

                // 受理されなかった要求には完了イベントが来ないため、待たずに欠損へ確定する
                // A rejected request has no completion event, so settle it as missing immediately
                if (!request.Accepted)
                {
                    _progress.FinishServerCapture();
                    data.AddMissing("serverSnapshot", $"サーバーが即時スナップショット要求を受け付けなかった reason:{request.RejectedReason}");
                    Publish();
                    return;
                }

                data.CaptureId = request.CaptureId;
                Publish();
                await _sources.WaitServerCaptureTimeout();
                if (beginCount != _beginCount || !_progress.IsServerCapturePending()) return;

                _progress.FinishServerCapture();
                data.AddMissing("serverSnapshot", $"完了イベントが {BugReportCaptureSession.ServerCaptureTimeoutSeconds}s 以内に届かなかった captureId:{request.CaptureId}");
                Publish();
            }

            #endregion
        }

        // 書き出し完了イベント。ID一致分のみ取込
        // Write-completed event; takes in only matching-id ones
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
                _data.AddMissing("serverSnapshot", $"サーバーがスナップショットの書き出しに失敗した captureId:{completion.CaptureId}");
                Publish();
                return;
            }

            // 縮退を残し剪定対象だけ作業場へ退避
            // Preserve degradation, stage only pruning targets
            if (!string.IsNullOrEmpty(completion.PacketLogDegradeReason)) _data.AddMissing("packetLog", $"サーバーのパケット記録が縮退した reason:{completion.PacketLogDegradeReason} degradedAtTick:{completion.PacketLogDegradedAtTick}");
            _data.ReportTick = completion.Tick;
            _data.ServerDataDirectory = completion.ServerDataDirectory;
            _data.WorldRootDirectory = string.IsNullOrEmpty(completion.SnapshotDirectory) ? null : Path.GetDirectoryName(completion.SnapshotDirectory);
            _progress.BeginStaging();
            StageServerCapture().Forget();
            Publish();

            #region Internal

            async UniTaskVoid StageServerCapture()
            {
                var beginCount = _beginCount;
                var data = _data;
                var staged = await _sources.StageServerCapture(data.CaptureWorkDirectory, completion.SnapshotDirectory, completion.SnapshotFileNames, completion.PacketLogFileNames);
                if (beginCount != _beginCount) return;

                _progress.FinishStaging();
                data.StagedSnapshotDirectory = staged.StagingDirectory;
                data.SnapshotFileNames = new System.Collections.Generic.List<string>(staged.SnapshotFileNames);
                data.PacketLogFileNames = new System.Collections.Generic.List<string>(staged.PacketLogFileNames);
                foreach (var item in staged.Missing) data.AddMissing(item.Item, item.Reason);
                Publish();
            }

            #endregion
        }

        private void Publish()
        {
            _progress.Publish(_data, _submitGate);
        }
    }
}
