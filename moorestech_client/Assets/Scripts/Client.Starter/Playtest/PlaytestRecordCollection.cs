using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// 同意対象の記録収集を判断する。終了印は独立。
    /// Decides consent-gated record collection; exit marks stay independent.
    /// </summary>
    public static class PlaytestRecordCollection
    {
        // 集めないのはリモート接続だけ。同意はタイトルで済んでいるので UI の起動の状態は条件にしない（ADR 0065）
        // Only a remote connection collects nothing; the consent was settled at the title, so the UI boot state is not a condition
        public static bool Decide(bool isRemoteConnection)
        {
            if (!isRemoteConnection) return true;
            Debug.Log("PlaytestRecordCollection: リモート接続のためプレイテストの記録（ログ・録画・進行記録）を集めません。終了の印は記録します");
            return false;
        }
    }
}
