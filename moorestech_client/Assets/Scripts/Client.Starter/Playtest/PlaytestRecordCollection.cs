using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// プレイテストの記録（ログ・録画・バグ報告の確保・進行記録・正常終了の印）をこの起動で集めるかを1箇所で決める。
    /// Decides in one place whether this boot collects playtest records (logs, recording, bug-report capture, progress record, clean-exit marks).
    /// </summary>
    public static class PlaytestRecordCollection
    {
        // 集めないのはリモート接続だけ。同意はタイトルで済んでいるので WebUiHost の起動有無は条件にしない（ADR 0065）
        // Only a remote connection collects nothing; the consent was settled at the title, so whether WebUiHost started is no longer a condition (ADR 0065)
        public static bool Decide(bool isRemoteConnection)
        {
            if (!isRemoteConnection) return true;
            Debug.Log("PlaytestRecordCollection: リモート接続のためプレイテストの記録（ログ・録画・進行記録・終了の印）を集めません");
            return false;
        }
    }
}
