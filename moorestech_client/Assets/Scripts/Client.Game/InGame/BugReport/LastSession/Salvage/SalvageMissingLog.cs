using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 退避の欠損を積む先。必ず開発者ログと報告の両方へ積み、片方だけで理由が誰にも届かない状態を作らない
    // Where the salvage's gaps accumulate; every gap lands in both the developer log and the report so the reason never reaches only one side
    internal sealed class SalvageMissingLog
    {
        public List<MissingItem> Items { get; } = new();

        public void Report(string item, string reason)
        {
            Debug.LogWarning($"前回セッションの退避で欠損 {item}: {reason}");
            Items.Add(new MissingItem { Item = item, Reason = reason });
        }
    }
}
