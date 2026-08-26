namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット画面用サブステートIF
    // Sub-state interface for the skit screen
    public interface ISkitScreenSubState
    {
        void OnEnter();
        
        // 遷移先を返す。nullなら継続
        // Return the next sub-state, or null to stay
        SkitScreenUIStateEnum? GetNextUpdate();
        
        void OnExit();
    }
}
