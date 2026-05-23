namespace Client.Game.InGame.Player.StateController
{
    // プレイヤーステートの共通インターフェース。遷移トリガーは UIStateControl 側に持つため
    // GetNextUpdate は持たない（依存方向: UIState → PlayerStateController）。
    // Common interface for player states. Transitions are driven by UIStateControl, so
    // no GetNextUpdate (dependency direction: UIState → PlayerStateController).
    public interface IPlayerState
    {
        void OnEnter();
        void Tick();
        void OnExit();
    }
}
