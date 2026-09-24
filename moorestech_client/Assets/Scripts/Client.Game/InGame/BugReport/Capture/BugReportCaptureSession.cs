using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BugReport.Recording;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Capture
{
    // 開始・成功の瞬間を確保しサーバー完了を待つ
    // Captures pause-open/send-success moments, waits for server completion
    public sealed class BugReportCaptureSession
    {
        public const float ServerCaptureTimeoutSeconds = 15f;
        private readonly IBugReportCaptureSources _sources;
        private readonly BugReportCaptureProgress _progress = new();
        private readonly BugReportServerCaptureCoordinator _serverCaptureCoordinator;
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
            _serverCaptureCoordinator = new BugReportServerCaptureCoordinator(_sources, _progress, _submitGate);
        }

        // ポーズメニューを開いた瞬間に呼ばれ、その時点の記録一式を確保する
        // Called the moment the pause menu opens; secures every record as of that instant
        public void BeginOnPauseMenu()
        {
            BeginCapture();
        }

        // 記録一式を新しい確保世代として開始する
        // Starts every record as a new capture generation
        private void BeginCapture()
        {
            _beginCount++;
            // 前回の確保はこの時点で送り直せなくなる。作業場を残すとEscapeのたびに丸ごと積み上がる
            // The previous capture can no longer be re-sent from here, so keeping its workspace would pile one up per Escape
            ReleasePreviousWorkspace();
            var data = new BugReportCapturedData { CaptureWorkDirectory = BugReportCaptureWorkspace.Create() };
            _data = data;
            _progress.BeginCapture();
            _submitGate.Reset();
            // 記録境界の確定とサーバーへの要求を先に出す。エンコーダーの排出待ちは続きへ回し、Escapeで画面を止めない
            // The recording boundary and the server request go out first; the encoder drain waits in a continuation so Escape never freezes the screen
            var recording = _sources.TakeRecordingAtCapture();
            _serverCaptureCoordinator.BeginCapture(data, _beginCount);
            TakeRecording(recording, data, _beginCount).Forget();
            CaptureScreenshot(data, _beginCount).Forget();
            data.Logs = _sources.Logs();
            data.ClientState = _sources.ClientState();
            data.ReportTick = data.ClientState.Tick;
            // 取れなかったカメラ・プレイヤーは原点という実値ではなく欠損として残す
            // A camera or player that could not be read is recorded as missing, never as a real position at the origin
            if (!data.ClientState.HasCamera) data.AddMissing("cameraState", "メインカメラが無く、カメラの位置と向きを確保できなかった");
            if (!data.ClientState.HasPlayer) data.AddMissing("playerState", "プレイヤーが無く、位置を確保できなかった");
            _progress.Publish(_data, _submitGate);
        }

        // サーバーの書き出し完了イベント。要求IDが一致するものだけを取り込む
        // The server's write-completed event; only the matching request id is taken in
        public void OnServerCaptureCompleted(ServerCaptureCompletion completion)
        {
            _serverCaptureCoordinator.OnServerCaptureCompleted(completion);
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
                // 差し替え時に消せなかった作業場はこの送信の持ち物。ここが最後の解放点になる
                // A workspace the swap could not drop belongs to this send, so this is its last release point
                Debug.LogWarning("バグ報告: 別の確保に差し替わった後の送信結果なので確保状態へ戻しません");
                BugReportCaptureWorkspace.Delete(data.CaptureWorkDirectory);
                return;
            }

            _submitGate.Complete();
            // 成功した記録は次へ引き継がず、その瞬間の一式を確保する。失敗なら元の資料で再試行する
            // A successful send starts fresh records at this instant; a failed send retries the original materials
            if (ready)
            {
                BeginCapture();
                return;
            }
            _data.Missing.Clear();
            _data.Missing.AddRange(missing);
            _progress.Publish(_data, _submitGate);
        }

        // 送信中の作業場だけは消さない（書き出しが読んでいる最中）。その分はCompleteSubmitが引き取る
        // Only a workspace with a send in flight survives, since the writer is still reading it; CompleteSubmit reclaims that one
        private void ReleasePreviousWorkspace()
        {
            var keep = _data != null && _submitGate.IsInFlight ? _data.CaptureWorkDirectory : null;
            if (keep != null) Debug.Log($"バグ報告: 送信中のため前回の作業場は送信完了まで残します directory:{keep}");
            BugReportCaptureWorkspace.DeleteAllExcept(keep);
        }

        // 境界は呼び出し時点で確定済み。ここで待つのはエンコーダーが区間を吐き終えるまで
        // The boundary is already settled at the call; this only waits for the encoder to finish flushing the segments
        private async UniTaskVoid TakeRecording(UniTask<CapturedRecording> recording, BugReportCapturedData data, int beginCount)
        {
            var captured = await recording;
            if (beginCount != _beginCount) return;
            _progress.FinishRecording();
            if (!captured.IsAvailable) data.AddMissing("video", captured.UnavailableReason);
            data.VideoSegmentFiles = captured.SegmentFiles.ToList();
            data.FrameTicks = captured.FrameTicks;
            _progress.Publish(_data, _submitGate);
        }

        private async UniTaskVoid CaptureScreenshot(BugReportCapturedData data, int beginCount)
        {
            var path = await _sources.CaptureScreenshot(data.CaptureWorkDirectory);
            if (beginCount != _beginCount) return;
            _progress.FinishScreenshot();
            if (path == null) data.AddMissing("screenshot", "スクリーンショットの書き出しに失敗した");
            data.ScreenshotPath = path;
            _progress.Publish(_data, _submitGate);
        }
    }
}
