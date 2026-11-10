using System;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// ゲームの層を覆う画面かの判定。Web の deriveActiveLayer（modal > blockInventory > research > buildMenu > game）の写し
    /// Whether a screen covers the game layer; a copy of the web deriveActiveLayer (modal > blockInventory > research > buildMenu > game)
    /// </summary>
    public static class UiActiveLayer
    {
        public static bool CoversGameLayer(UiScreen screen, bool modalPending)
        {
            if (modalPending) return true;

            // 全 UiScreen を列挙する。新しい画面は分類しないと例外で落ち、書き忘れが無警告にならない
            // Every UiScreen is listed; an unclassified new screen throws so it cannot be forgotten silently
            switch (screen)
            {
                case UiScreen.SubInventory:
                case UiScreen.ResearchTree:
                case UiScreen.BuildMenu:
                    return true;
                case UiScreen.None:
                case UiScreen.PlayerInventory:
                case UiScreen.ChallengeList:
                case UiScreen.PauseMenu:
                case UiScreen.TrainHud:
                case UiScreen.TrainPause:
                case UiScreen.SkitPause:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen), screen, "UiScreen is not classified in UiActiveLayer");
            }
        }
    }
}
