namespace Client.Game.InGame.Player.StateController
{
    // プレイヤーステートの共通インターフェース。遷移トリガーは外部 (UIState 側) に持つため
    // GetNextUpdate は持たない（依存方向: 外部 → PlayerStateController）。
    // Common interface for player states. Transitions are driven externally (UIState side),
    // so no GetNextUpdate (dependency direction: external → PlayerStateController).
    public interface IPlayerState
    {
        void OnEnter(IPlayerStateContext context);
        void Tick();
        void OnExit();
    }
}
