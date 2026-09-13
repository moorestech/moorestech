using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport.Recording;
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
        private readonly BugReportCaptureProgress _progress = new();

        private BugReportCapturedData _data;

        // 同じ確保から2箱作ると同じ報告のdraft PRが2本出るので、送信の可否は専用の門が持つ
        // Two boxes from one capture raise two draft PRs for one report, so a dedicated gate owns whether a send may start
        private readonly BugReportSubmitGate _submitGate = new();

        // 何回目の確保かを持ち、非同期の続きが古い確保のものかを判定する
        // Counts begins so an async continuation can tell whether it belongs to a stale capture
        private int _beginCount;

        public IReadOnlyReactiveProperty<BugReportCaptureStatus> Status => _progress.Status;

        public BugReportCaptureSession(IBugReportCaptureSources sources)
        {
            _sources = sources;
        }

        // ポーズメニューを開いた瞬間に呼ばれ、その時点の記録一式を確保する
        // Called the moment the pause menu opens; secures every record as of that instant
        public void BeginOnPauseMenu()
        {
            _beginCount++;
            var data = new BugReportCapturedData { CaptureWorkDirectory = BugReportCaptureWorkspace.Create() };
            _data = data;
            _progress.BeginCapture();
            _submitGate.Reset();

            // 記録境界の確定とサーバーへの要求を先に出す。エンコーダーの排出待ちは続きへ回し、Escapeで画面を止めない
            // The recording boundary and the server request go out first; the encoder drain waits in a continuation so Escape never freezes the screen
            var recording = _sources.TakeRecordingAtCapture();
            RequestServerCapture(data, _beginCount).Forget();
            TakeRecording(recording, data, _beginCount).Forget();
            CaptureScreenshot(data, _beginCount).Forget();

            data.Logs = _sources.Logs();
            data.ClientState = _sources.ClientState();
            data.ReportTick = data.ClientState.Tick;

            // 取れなかったカメラ・プレイヤーは原点という実値ではなく欠損として残す
            // A camera or player that could not be read is recorded as missing, never as a real position at the origin
            if (!data.ClientState.HasCamera) AddMissing(data, "cameraState", "メインカメラが無く、カメラの位置と向きを確保できなかった");
            if (!data.ClientState.HasPlayer) AddMissing(data, "playerState", "プレイヤーが無く、位置を確保できなかった");

            _progress.Publish(_data, _submitGate);
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
                _progress.Publish(_data, _submitGate);
                return;
            }

            // サーバーが申告した縮退は握り潰さない。区間が揃っていてもパケットが欠けていることは報告側でしか分からない
            // A degradation the server declared is never swallowed; only the report side can tell that packets are missing despite complete segments
            if (!string.IsNullOrEmpty(completion.PacketLogDegradeReason)) AddMissing(_data, "packetLog", $"サーバーのパケット記録が縮退した reason:{completion.PacketLogDegradeReason} degradedAtTick:{completion.PacketLogDegradedAtTick}");

            _data.ReportTick = completion.Tick;
            _data.ServerDataDirectory = completion.ServerDataDirectory;

            // ワールド定義は剪定されないので置き場のまま持つ。剪定されるスナップショットと区間だけを実体退避する
            // The world definition is never pruned so its root is kept as is; only the prunable snapshots and segments are staged
            _data.WorldRootDirectory = string.IsNullOrEmpty(completion.SnapshotDirectory) ? null : Path.GetDirectoryName(completion.SnapshotDirectory);
            _progress.BeginStaging();
            StageServerCapture(_data, completion, _beginCount).Forget();
            _progress.Publish(_data, _submitGate);
        }

        // 送信してよいかを判定し、許可なら記録一式を渡して送信中にする。判定の権威はここ1箇所
        // Decides whether a send may start and hands over the records; this is the single authority for that decision
        public BugReportSubmitTicket TryBeginSubmit()
        {
            return _submitGate.TryBegin(_data, _progress.IsCapturePending());
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

            // 箱へ写し終えた確保の作業場は残さない。書けなかった確保は送り直せるよう残す
            // The workspace is dropped once its materials reached the box; a failed write keeps them so the send can be retried
            if (ready) BugReportCaptureWorkspace.Delete(data.CaptureWorkDirectory);
            _progress.Publish(_data, _submitGate);
        }

        private async UniTaskVoid RequestServerCapture(BugReportCapturedData data, int beginCount)
        {
            var request = await _sources.RequestServerCapture();
            if (beginCount != _beginCount) return;
            // 受理されなかった要求には完了イベントが来ない。待たずに欠損として確定させる
            // A rejected request never gets a completion event, so finish it as missing instead of waiting
            if (!request.Accepted)
            {
                _progress.FinishServerCapture();
                AddMissing(data, "serverSnapshot", $"サーバーが即時スナップショット要求を受け付けなかった reason:{request.RejectedReason}");
                _progress.Publish(_data, _submitGate);
                return;
            }

            data.CaptureId = request.CaptureId;
            _progress.Publish(_data, _submitGate);

            await _sources.WaitServerCaptureTimeout();
            if (beginCount != _beginCount || !_progress.IsServerCapturePending()) return;

            _progress.FinishServerCapture();
            AddMissing(data, "serverSnapshot", $"完了イベントが {ServerCaptureTimeoutSeconds}s 以内に届かなかった captureId:{request.CaptureId}");
            _progress.Publish(_data, _submitGate);
        }

        // 境界は呼び出し時点で確定済み。ここで待つのはエンコーダーが区間を吐き終えるまで
        // The boundary is already settled at the call; this only waits for the encoder to finish flushing the segments
        private async UniTaskVoid TakeRecording(UniTask<CapturedRecording> recording, BugReportCapturedData data, int beginCount)
        {
            var captured = await recording;
            if (beginCount != _beginCount) return;
            _progress.FinishRecording();
            if (!captured.IsAvailable) AddMissing(data, "video", captured.UnavailableReason);
            data.VideoSegmentFiles = captured.SegmentFiles.ToList();
            data.FrameTicks = captured.FrameTicks;
            _progress.Publish(_data, _submitGate);
        }

        // 記入中もサーバーの剪定は進むため、Escape時点の記録は名前ではなく実体で確保する（ADR 0057）
        // The server keeps pruning while the user types, so the Escape-moment records are secured as files, not names (ADR 0057)
        private async UniTaskVoid StageServerCapture(BugReportCapturedData data, ServerCaptureCompletion completion, int beginCount)
        {
            var staged = await _sources.StageServerCapture(data.CaptureWorkDirectory, completion.SnapshotDirectory, completion.SnapshotFileNames, completion.PacketLogFileNames);
            if (beginCount != _beginCount) return;
            _progress.FinishStaging();
            data.StagedSnapshotDirectory = staged.StagingDirectory;
            data.SnapshotFileNames = staged.SnapshotFileNames.ToList();
            data.PacketLogFileNames = staged.PacketLogFileNames.ToList();
            foreach (var item in staged.Missing) AddMissing(data, item.Item, item.Reason);
            _progress.Publish(_data, _submitGate);
        }

        private async UniTaskVoid CaptureScreenshot(BugReportCapturedData data, int beginCount)
        {
            var path = await _sources.CaptureScreenshot(data.CaptureWorkDirectory);
            if (beginCount != _beginCount) return;
            _progress.FinishScreenshot();
            if (path == null) AddMissing(data, "screenshot", "スクリーンショットの書き出しに失敗した");
            data.ScreenshotPath = path;
            _progress.Publish(_data, _submitGate);
        }

        private static void AddMissing(BugReportCapturedData data, string item, string reason)
        {
            Debug.LogWarning($"バグ報告の記録が欠けます item:{item} reason:{reason}");
            data.Missing.Add(new MissingItem { Item = item, Reason = reason });
        }
    }
}
