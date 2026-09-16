namespace Client.Game.InGame.BugReport.DiskOperations
{
    // ディスク1操作ぶんの結果。成功したかと、しなかった理由（ディスク失敗・対象が無い/空）を分けて持ち帰る
    // One disk operation's result: whether it succeeded and, if not, why (disk failure, or a missing/empty target)
    public sealed class SalvageOperationResult
    {
        public bool Succeeded;
        public string FailureReason;

        public static SalvageOperationResult Success()
        {
            return new SalvageOperationResult { Succeeded = true };
        }

        public static SalvageOperationResult Failure(string reason)
        {
            return new SalvageOperationResult { FailureReason = reason };
        }
    }
}
