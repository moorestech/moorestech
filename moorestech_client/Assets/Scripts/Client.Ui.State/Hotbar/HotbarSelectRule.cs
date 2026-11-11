using Client.Game.InGame.UI.UIState;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// 枠の選択を受ける UI 状態の唯一の規則（Web uiStateAcceptsHotbarSelect の写し）
    /// The single rule for which UI states accept a slot selection (port of Web uiStateAcceptsHotbarSelect)
    /// </summary>
    public static class HotbarSelectRule
    {
        // 選択を消費する者がいる 2 状態だけ。他で積むと画面を閉じた瞬間に暴発する
        // Only the two states with a consumer; queuing elsewhere fires the moment that screen closes
        public static bool Accepts(UIStateEnum uiState)
        {
            return uiState == UIStateEnum.GameScreen || uiState == UIStateEnum.PlaceBlock;
        }
    }
}
