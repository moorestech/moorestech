using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Registration
{
    /// <summary>
    /// 記録を集めない起動の取得元。何も取らず、呼ばれたら理由をログへ出して「取れなかった」を返す。
    /// The capture source for a boot that collects nothing; it takes nothing and, if called, logs why and answers "unavailable".
    /// </summary>
    internal sealed class UncollectedBugReportCaptureSources : IBugReportCaptureSources
    {
        private const string Reason = "この起動はプレイテストの記録を集めない（リモート接続またはWebUI無し）";

        // 確保の起点を登録していないため本来は呼ばれない。呼ばれたら配線の変化なので無音にしない
        // No capture trigger is registered, so this is normally never called; a call means the wiring changed and is never silent
        public UniTask<BugReportServerCaptureRequest> RequestServerCapture()
        {
            Debug.LogWarning($"UncollectedBugReportCaptureSources: 確保が要求されましたが記録しません（{Reason}）");
            return UniTask.FromResult(new BugReportServerCaptureRequest(false, 0, Reason));
        }

        public void SetCurrentUiState(UIStateEnum uiState)
        {
        }

        public UniTask WaitServerCaptureTimeout()
        {
            return UniTask.CompletedTask;
        }

        public UniTask<StagedServerCapture> StageServerCapture(string workDirectory, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            return UniTask.FromResult(new StagedServerCapture(workDirectory, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<MissingItem>()));
        }

        public UniTask<CapturedRecording> TakeRecordingAtCapture()
        {
            return UniTask.FromResult(CapturedRecording.Unavailable(Reason));
        }

        public IReadOnlyList<UnityLogEntry> Logs()
        {
            return Array.Empty<UnityLogEntry>();
        }

        public ClientStateSnapshot ClientState()
        {
            return new ClientStateSnapshot(Vector3.zero, Vector3.zero, Vector3.zero, "", 0, false, false);
        }

        public UniTask<string> CaptureScreenshot(string workDirectory)
        {
            return UniTask.FromResult<string>(null);
        }
    }
}
