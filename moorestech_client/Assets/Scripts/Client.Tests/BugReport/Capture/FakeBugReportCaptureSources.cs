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
        public int CutCount;
        public RecordingAvailability Availability = RecordingAvailability.Available();
        public string ScreenshotPath = "/tmp/shot.png";
        public UIStateEnum CurrentUiState = UIStateEnum.PauseMenu;

        // 退避は既定で即座に終わる。待たせたいテストだけが Hold して完了の順序を作る
        // Staging settles immediately by default; only tests that need ordering hold it open
        public string StagingDirectory = "/tmp/staging";
        public List<string> StagedSnapshots;
        public List<string> StagedPacketLogs;
        public List<MissingItem> StagingMissing = new();
        public string StagedFromDirectory;

        private readonly UniTaskCompletionSource _timeout = new();
        private UniTaskCompletionSource<StagedServerCapture> _stagingHold;

        public void HoldStaging()
        {
            _stagingHold = new UniTaskCompletionSource<StagedServerCapture>();
        }

        public void ReleaseStaging()
        {
            _stagingHold.TrySetResult(Staged());
        }

        public void ElapseServerCaptureTimeout()
        {
            _timeout.TrySetResult();
        }

        public UniTask<BugReportServerCaptureRequest> RequestServerCapture() => UniTask.FromResult(RequestResult);
        public UniTask WaitServerCaptureTimeout() => _timeout.Task;

        public UniTask<StagedServerCapture> StageServerCapture(string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            StagedFromDirectory = snapshotDirectory;
            StagedSnapshots ??= snapshotFileNames.ToList();
            StagedPacketLogs ??= packetLogFileNames.ToList();
            return _stagingHold == null ? UniTask.FromResult(Staged()) : _stagingHold.Task;
        }

        private StagedServerCapture Staged()
        {
            return new StagedServerCapture(StagingDirectory, StagedSnapshots, StagedPacketLogs, StagingMissing);
        }
        public void CutRecordingSegment() => CutCount++;
        public IReadOnlyList<string> CompletedVideoSegments() => new List<string> { "/tmp/seg_00.mp4" };
        public RecordingAvailability GetRecordingAvailability() => Availability;
        public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks() => new List<(long, ulong)> { (1, 2) };
        public IReadOnlyList<UnityLogEntry> Logs() => new List<UnityLogEntry>();
        public void SetCurrentUiState(UIStateEnum uiState) => CurrentUiState = uiState;
        public ClientStateSnapshot ClientState() => new(Vector3.zero, Vector3.zero, Vector3.zero, CurrentUiState.ToString(), 2);
        public UniTaskCompletionSource<string> ScreenshotTask;
        public UniTask<string> CaptureScreenshot() => ScreenshotTask == null ? UniTask.FromResult(ScreenshotPath) : ScreenshotTask.Task;
    }
}
