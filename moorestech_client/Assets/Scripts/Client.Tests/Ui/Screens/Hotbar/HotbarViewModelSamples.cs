using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Ui.State.Hotbar;

namespace Client.Tests.Ui.Screens.Hotbar
{
    /// <summary>
    /// Web の mock-host の fixture（枠 0 が選択で絵あり、枠 4 が未解決、他は空き）の ViewModel
    /// View models for the Web mock-host fixture (slot 0 selected with a picture, slot 4 unresolved, the rest empty)
    /// </summary>
    internal static class HotbarViewModelSamples
    {
        public static readonly Guid UnresolvedId = new("0d3c2b1a-9f8e-4d7c-b6a5-948372615041");

        public static HotbarViewModel WebFixture(bool selectAccepted, bool visible)
        {
            var slots = Enumerable.Repeat<HotbarSlotViewModel>(HotbarEmptySlot.Instance, 9).ToArray();
            slots[0] = HotbarTestPresentation.PicturedBlueprint();
            slots[4] = new HotbarUnresolvedSlot(UnresolvedId);
            return new HotbarViewModel(slots, HotbarSelection.FromOrigin(PlacementOrigin.FromHotbarSlot(0)), selectAccepted, visible);
        }

        public static HotbarViewModel WebFixture()
        {
            return WebFixture(true, true);
        }
    }
}
