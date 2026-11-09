using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Routing
{
    // UITK 起点の UI 状態の遷移要求の口。拒んだら false（理由は実装がログへ出す）
    // Port for UITK-initiated UI-state transition requests; false when refused (the implementation logs why)
    public interface IUiStateTransitionRequester
    {
        bool RequestTransition(UIStateEnum requested);
    }
}
