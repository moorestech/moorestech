using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Tests.BugReport.Capture
{
    // 確保セッションの外部依存を全部握るフェイク。待ちはテストが任意の時点で解く
    // Fake holding every external dependency; the test releases the wait whenever it wants
    public sealed class FakeBugReportCaptureSources : IBugReportCaptureSources
    {
        public BugReportServerCaptureRequest RequestResult = new(true, 7, null);
        public string ScreenshotPath = "/tmp/shot.png";
        public UIStateEnum CurrentUiState = UIStateEnum.PauseMenu;
        public bool HasCamera = true;
        public bool HasPlayer = true;

        // 録画は既定で即座に確定する。境界の確定と排出待ちを分けたいテストだけが Hold する
        // The recording settles immediately by default; only tests separating the boundary from the drain hold it open
        public CapturedRecording Recording = CapturedRecording.Available(new List<string> { "/tmp/seg_00.mp4" }, new List<FrameTickRow> { new(1, 2, 0, 0) });
        public int TakeRecordingCount;

        // 退避は既定で即座に終わる。待たせたいテストだけが Hold して完了の順序を作る
        // Staging settles immediately by default; only tests that need ordering hold it open
        public string StagingDirectory = "/tmp/staging";
        public List<string> StagedSnapshots;
        public List<string> StagedPacketLogs;
        public List<MissingItem> StagingMissing = new();
        public string StagedFromDirectory;
        public string StagedWorkDirectory;
        public string ScreenshotWorkDirectory;

        private readonly UniTaskCompletionSource _timeout = new();
        private UniTaskCompletionSource<StagedServerCapture> _stagingHold;
        private UniTaskCompletionSource<CapturedRecording> _recordingHold;

        public void HoldStaging()
        {
            _stagingHold = new UniTaskCompletionSource<StagedServerCapture>();
        }

        public void ReleaseStaging()
        {
            _stagingHold.TrySetResult(Staged());
        }

        public void HoldRecording()
        {
            _recordingHold = new UniTaskCompletionSource<CapturedRecording>();
        }

        public void ReleaseRecording()
        {
            _recordingHold.TrySetResult(Recording);
        }

        public void ElapseServerCaptureTimeout()
        {
            _timeout.TrySetResult();
        }

        public UniTask<BugReportServerCaptureRequest> RequestServerCapture() => UniTask.FromResult(RequestResult);
        public UniTask WaitServerCaptureTimeout() => _timeout.Task;

        public UniTask<StagedServerCapture> StageServerCapture(string workDirectory, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            StagedWorkDirectory = workDirectory;
            StagedFromDirectory = snapshotDirectory;
            StagedSnapshots ??= snapshotFileNames.ToList();
            StagedPacketLogs ??= packetLogFileNames.ToList();
            return _stagingHold == null ? UniTask.FromResult(Staged()) : _stagingHold.Task;
        }

        public UniTask<CapturedRecording> TakeRecordingAtCapture()
        {
            TakeRecordingCount++;
            return _recordingHold == null ? UniTask.FromResult(Recording) : _recordingHold.Task;
        }

        private StagedServerCapture Staged()
        {
            return new StagedServerCapture(StagingDirectory, StagedSnapshots, StagedPacketLogs, StagingMissing);
        }
        public IReadOnlyList<UnityLogEntry> Logs() => new List<UnityLogEntry>();
        public void SetCurrentUiState(UIStateEnum uiState) => CurrentUiState = uiState;
        public ClientStateSnapshot ClientState() => new(Vector3.zero, Vector3.zero, Vector3.zero, CurrentUiState.ToString(), 2, HasCamera, HasPlayer);
        public UniTaskCompletionSource<string> ScreenshotTask;

        public UniTask<string> CaptureScreenshot(string workDirectory)
        {
            ScreenshotWorkDirectory = workDirectory;
            return ScreenshotTask == null ? UniTask.FromResult(ScreenshotPath) : ScreenshotTask.Task;
        }
    }
}
