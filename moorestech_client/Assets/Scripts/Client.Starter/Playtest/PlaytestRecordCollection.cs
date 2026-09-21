using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// 同意表示を要する記録（ログ・録画・バグ報告の確保・進行記録）を集めるか決める。終了印はこの判断に依存しない。
    /// Decides whether to collect consent-gated records (logs, recording, bug-report capture, progress); exit marks are independent.
    /// </summary>
    public static class PlaytestRecordCollection
    {
        // 集めるのは同意ゲートを出せる起動だけ。何が送られるかを見せられない起動で記録を溜めない
        // Only a boot that can show the consent gate collects; a boot that cannot show what gets sent accumulates nothing
        public static bool Decide(bool isRemoteConnection, bool webUiHostStarted)
        {
            if (isRemoteConnection)
            {
                Debug.Log("PlaytestRecordCollection: リモート接続のためプレイテストの記録（ログ・録画・進行記録）を集めません。終了の印は記録します");
                return false;
            }
            if (!webUiHostStarted)
            {
                Debug.LogWarning("PlaytestRecordCollection: WebUiHostが起動しておらず同意表示を出せないため、プレイテストの記録（ログ・録画・進行記録）を集めません。終了の印は記録します");
                return false;
            }
            return true;
        }
    }
}
