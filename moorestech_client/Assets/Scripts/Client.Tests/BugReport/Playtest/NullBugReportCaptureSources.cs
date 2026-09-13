using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Tests.BugReport
{
    // 確保セッションを空で組むためのテスト用取得元。全て即座に既定値を返し何も記録しない
    // A test source that builds an empty capture session; every call resolves immediately with defaults and records nothing
    public sealed class NullBugReportCaptureSources : IBugReportCaptureSources
    {
        public UniTask<BugReportServerCaptureRequest> RequestServerCapture() => UniTask.FromResult(new BugReportServerCaptureRequest(true, 1, null));

        public void SetCurrentUiState(UIStateEnum uiState) { }

        public UniTask WaitServerCaptureTimeout() => UniTask.CompletedTask;

        public UniTask<StagedServerCapture> StageServerCapture(string workDirectory, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            return UniTask.FromResult(new StagedServerCapture(workDirectory, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<MissingItem>()));
        }

        public UniTask<CapturedRecording> TakeRecordingAtCapture() => UniTask.FromResult(CapturedRecording.Unavailable("テスト用の取得元なので録画しない"));

        public IReadOnlyList<UnityLogEntry> Logs() => Array.Empty<UnityLogEntry>();

        public ClientStateSnapshot ClientState() => new(Vector3.zero, Vector3.zero, Vector3.zero, "PauseMenu", 0, false, false);

        public UniTask<string> CaptureScreenshot(string workDirectory) => UniTask.FromResult<string>(null);
    }
}
