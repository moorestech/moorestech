using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// プレイテストの記録（ログ・録画・バグ報告の確保・進行記録・正常終了の印）をこの起動で集めるかを1箇所で決める。
    /// Decides in one place whether this boot collects playtest records (logs, recording, bug-report capture, progress record, clean-exit marks).
    /// </summary>
    public static class PlaytestRecordCollection
    {
        // 集めるのは同意ゲートを出せる起動だけ。何が送られるかを見せられない起動で記録を溜めない
        // Only a boot that can show the consent gate collects; a boot that cannot show what gets sent accumulates nothing
        public static bool Decide(bool isRemoteConnection, bool webUiHostStarted)
        {
            if (isRemoteConnection)
            {
                Debug.Log("PlaytestRecordCollection: リモート接続のためプレイテストの記録（ログ・録画・進行記録・終了の印）を集めません");
                return false;
            }
            if (!webUiHostStarted)
            {
                Debug.LogWarning("PlaytestRecordCollection: WebUiHostが起動しておらず同意表示を出せないため、プレイテストの記録（ログ・録画・進行記録・終了の印）を集めません");
                return false;
            }
            return true;
        }
    }
}
