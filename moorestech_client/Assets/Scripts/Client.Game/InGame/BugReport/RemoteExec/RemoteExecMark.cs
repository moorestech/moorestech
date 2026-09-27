using System.Collections.Generic;

namespace Client.Game.InGame.BugReport
{
    // 取り込み側へ渡す遠隔実行の有効印と箱内の台帳パス
    // Remote execution flag and bundle-relative ledger paths sent to ingestion
    public sealed class RemoteExecMark
    {
        public bool Enabled;
        public List<string> LedgerFiles = new();
    }
}
