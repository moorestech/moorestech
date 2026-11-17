using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 現状態から要求できる遷移の唯一の表（UiStateControlTransitionRequester と各 View が呼ぶ）
    /// The single table of transitions requestable from the current state (called by UiStateControlTransitionRequester and the Views)
    /// </summary>
    public static class UiStateTransitionRules
    {
        // 遷移要求の命令名（View の拒否ログとトーストの診断に使う）
        // Command name of the transition request (used by Views for refusal log and toast diagnostics)
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
