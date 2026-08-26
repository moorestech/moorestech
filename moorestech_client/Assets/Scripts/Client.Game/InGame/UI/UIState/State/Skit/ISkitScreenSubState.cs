namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット画面専用のサブステートインターフェース。ITrainHudScreenSubStateと同型
    // Sub-state interface for the skit screen. Same shape as ITrainHudScreenSubState
    public interface ISkitScreenSubState
    {
        void OnEnter();
        
        // 別のサブステートへ遷移する場合は遷移先を返す。nullなら継続
        // Return the next sub-state to transit to, or null to stay in the current one
        SkitScreenUIStateEnum? GetNextUpdate();
        
        void OnExit();
    }
}
