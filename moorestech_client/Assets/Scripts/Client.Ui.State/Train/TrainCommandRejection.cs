using Client.Ui.State.Commands;
using UnityEngine;

namespace Client.Ui.State.Train
{
    /// <summary>
    /// 列車系コマンドの拒否を理由ごとの値で作り、Web ホストと同じ書式で開発者ログへ出す
    /// Builds train command refusals with per-reason values and logs them for developers in the Web host's format
    /// </summary>
    public static class TrainCommandRejection
    {
        public static UiCommandRejected Reject(string logTag, UiCommandRejectReason reason, string serverFailureReason)
        {
            // Detail はサーバーの失敗理由だけ。応答が無い・理由が無い拒否では呼び手が空文字を渡す（前例 PauseMenuCommands.SubmitBugReport）
            // Detail carries only the server failure reason; callers pass empty when there is no response or reason (precedent PauseMenuCommands.SubmitBugReport)
            var detail = serverFailureReason;
            var wire = UiCommandRejectReasonTraits.ToWireCode(reason);

            // Web ホストの TrainTimetableActionSupport.Reject と同じ「理由:失敗理由」の書式
            // Same "reason:failure" format as the Web host's TrainTimetableActionSupport.Reject
            Debug.LogWarning(detail.Length == 0 ? $"[{logTag}] rejected: {wire}" : $"[{logTag}] rejected: {wire}:{detail}");
            return new UiCommandRejected(reason, detail);
        }
    }
}
