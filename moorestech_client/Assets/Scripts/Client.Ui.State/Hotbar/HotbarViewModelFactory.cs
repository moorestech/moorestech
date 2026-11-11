using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// 枠・由来・UI 状態・画面・スキットから ViewModel を組む純関数
    /// Pure function building the view model from slots, origin, UI state, screen and skit
    /// </summary>
    public static class HotbarViewModelFactory
    {
        public static HotbarViewModel Create(IReadOnlyList<HotbarSlotViewModel> slots, PlacementOrigin origin, UIStateEnum uiState, UiScreen screen, bool blockingSkit)
        {
            // 常駐 HUD を出す画面で、blocking スキットの間だけ退く（Web App.tsx の screenShowsAlwaysOnHud と HotbarPanel の useBlockingSkitActive）
            // Shows on screens that show always-on HUDs and retreats only during a blocking skit (Web App.tsx screenShowsAlwaysOnHud and HotbarPanel useBlockingSkitActive)
            var visible = UiScreenTraitsTable.Get(screen).AlwaysOnHud && !blockingSkit;
            return new HotbarViewModel(slots, HotbarSelection.FromOrigin(origin), HotbarSelectRule.Accepts(uiState), visible);
        }
    }
}
