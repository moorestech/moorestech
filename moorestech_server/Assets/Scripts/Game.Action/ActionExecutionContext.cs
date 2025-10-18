namespace Game.Action
{
    /// <summary>
    /// アクション実行時のコンテキスト情報を保持する。
    /// 現状はアクション発火元プレイヤー ID の伝搬に利用する。
    /// </summary>
    public readonly struct ActionExecutionContext
    {
        public bool HasActionInvoker => ActionInvokerPlayerId.HasValue;
        public int? ActionInvokerPlayerId { get; }

        public static ActionExecutionContext ForPlayer(int playerId)
        {
            return new ActionExecutionContext(playerId);
        }

        public ActionExecutionContext(int? actionInvokerPlayerId)
        {
            ActionInvokerPlayerId = actionInvokerPlayerId;
        }
    }
}
