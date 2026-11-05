using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.NestedPause;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// UIState と入れ子サブステートから画面を解決する単一の正
    /// Single source that resolves the screen from UIState and the nested sub-state
    /// </summary>
    public static class UiScreenRouting
    {
        public static UiScreen Resolve(UIStateEnum state, NestedPauseSubStateEnum? subState)
        {
            // Web の screenForUiState と同じ写像。未対応 state はパネル無し
            // Mirrors the web screenForUiState; unsupported states are panel-less
            var nestedPause = subState == NestedPauseSubStateEnum.PauseMenuScreen;
            switch (state)
            {
                case UIStateEnum.PlayerInventory: return UiScreen.PlayerInventory;
                case UIStateEnum.SubInventory: return UiScreen.SubInventory;
                case UIStateEnum.ResearchTree: return UiScreen.ResearchTree;
                case UIStateEnum.BuildMenu: return UiScreen.BuildMenu;
                case UIStateEnum.ChallengeList: return UiScreen.ChallengeList;
                case UIStateEnum.PauseMenu: return UiScreen.PauseMenu;
                case UIStateEnum.TrainHUDScreen: return nestedPause ? UiScreen.TrainPause : UiScreen.TrainHud;
                // スキット本体は別経路で描くので、Story はポーズ入れ子だけを画面に昇格させる
                // The skit itself is drawn elsewhere, so Story only promotes its nested pause to a screen
                case UIStateEnum.Story: return nestedPause ? UiScreen.SkitPause : UiScreen.None;
                default: return UiScreen.None;
            }
        }
    }
}
