namespace Client.Game.InGame.UI.UIState.State.TrainHUDScreen
{
    // 列車HUD専用のサブステートインターフェース。IUIStateの簡易版
    // Sub-state interface for the train HUD. A simplified counterpart of IUIState.
    public interface ITrainHudScreenSubState
    {
        void OnEnter();

        // 別のサブステートへ遷移する場合は遷移先を返す。nullなら継続
        // Return the next sub-state to transit to, or null to stay in the current one.
        TrainHudScreenUIStateEnum? GetNextUpdate();

        void OnExit();
    }
}
