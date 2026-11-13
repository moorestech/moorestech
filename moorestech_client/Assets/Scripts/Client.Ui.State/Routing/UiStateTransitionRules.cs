using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 現状態から要求できる遷移の表（Web ホスト RequestUiStateActionHandler.IsAllowed の写し。重複は D-5 と同じ扱い）
    /// Table of transitions requestable from the current state (port of the Web host IsAllowed; duplication handled as in D-5)
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
