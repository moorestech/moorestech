namespace Game.SaveLoad.Migration.Steps.V3ToV4
{
    /// <summary>
    /// 1ブロックの接続への種類補填の結果。成功なら補填件数、失敗なら辿れなかった理由を持つ
    /// Result of filling tools into one block's connections: the filled count on success, the unwalkable reason on failure
    /// </summary>
    public sealed class ConnectionToolGuidFillResult
    {
        public bool IsFilled { get; }
        public int FilledCount { get; }
        public string FailureReason { get; }

        private ConnectionToolGuidFillResult(bool isFilled, int filledCount, string failureReason)
        {
            IsFilled = isFilled;
            FilledCount = filledCount;
            FailureReason = failureReason;
        }

        public static ConnectionToolGuidFillResult Filled(int filledCount)
        {
            return new ConnectionToolGuidFillResult(true, filledCount, null);
        }

        public static ConnectionToolGuidFillResult Failed(string reason)
        {
            return new ConnectionToolGuidFillResult(false, 0, reason);
        }
    }
}
