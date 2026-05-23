namespace Client.Game.InGame.Player.StateController
{
    // プレイヤーステート遷移時に渡すコンテキスト。現状は遷移元のみ保持。
    // Context passed on player-state transition; currently only holds the previous state.
    public class PlayerTransitContext
    {
        public PlayerStateEnum LastStateEnum { get; private set; }

        public PlayerTransitContext(PlayerStateEnum lastStateEnum)
        {
            LastStateEnum = lastStateEnum;
        }
    }
}
