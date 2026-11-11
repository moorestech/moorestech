using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ViewModel を組む純関数
    /// Pure function building the view model
    /// </summary>
    public static class HotbarViewModelFactory
    {
        public static HotbarViewModel Create(IReadOnlyList<HotbarSlotViewModel> slots, PlacementOrigin origin, UIStateEnum uiState, UiScreen screen, bool blockingSkit)
        {
            // 常駐 HUD の画面で blocking スキット中だけ退く
            // Retreats only during a blocking skit on always-on HUD screens
            var visible = UiScreenTraitsTable.Get(screen).AlwaysOnHud && !blockingSkit;
            return new HotbarViewModel(slots, HotbarSelection.FromOrigin(origin), HotbarSelectRule.Accepts(uiState), visible);
        }
    }
}
