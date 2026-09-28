using System.Collections.Generic;

namespace Client.Game.InGame.BugReport
{
    // 非nullなら有効。箱内の台帳パスを取り込み側へ渡す
    // A non-null mark means enabled and carries bundle-relative ledger paths
    public sealed class RemoteExecMark
    {
        public List<string> LedgerFiles = new();
    }
}
