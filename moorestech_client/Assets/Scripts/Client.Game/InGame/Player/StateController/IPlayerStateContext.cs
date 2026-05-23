namespace Client.Game.InGame.Player.StateController
{
    // プレイヤーステート遷移時に渡される context のマーカー基底。
    // 各 State 用の具象 context は派生で定義する（例: RidingPlayerStateContext）。
    // Marker base for contexts passed at player-state transitions.
    // Concrete contexts derive from this (e.g. RidingPlayerStateContext).
    public interface IPlayerStateContext
    {
    }
}
