using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 現状態から要求できる遷移の唯一の表（UITK の遷移要求と Web ホストの ui_state.request が呼ぶ。DC-23 A）
    /// The single table of transitions requestable from the current state (called by the UITK transition requester and the Web host ui_state.request; DC-23 A)
    /// </summary>
    public static class UiStateTransitionRules
    {
        // Web ホストの ui_state.request の命令名（View の拒否ログとトーストの診断に使う）
        // Command name of the Web host ui_state.request (used by Views for refusal log and toast diagnostics)
        public const string RequestCommandName = "ui_state.request";

        public static bool IsAllowed(UIStateEnum current, UIStateEnum requested)
        {
            if (current == requested) return true;
            return current switch
            {
                UIStateEnum.GameScreen => requested == UIStateEnum.PlayerInventory,
                UIStateEnum.PlayerInventory => requested == UIStateEnum.GameScreen,
                UIStateEnum.SubInventory => requested == UIStateEnum.GameScreen,
                UIStateEnum.BuildMenu => requested == UIStateEnum.GameScreen,
                UIStateEnum.ResearchTree => requested == UIStateEnum.GameScreen,
                UIStateEnum.ChallengeList => requested == UIStateEnum.GameScreen,
                UIStateEnum.PauseMenu => requested == UIStateEnum.GameScreen,
                _ => false,
            };
        }
    }
}
