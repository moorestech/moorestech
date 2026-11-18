using Client.Ui.State.Items;
using Client.Ui.State.Routing;

namespace Client.Ui.State.Inventory.Grab
{
    /// <summary>
    /// 掴み絵の表示を決める（Web GrabOverlay の useGrabInteractive() && grab.count > 0）
    /// Decides the grab picture (Web GrabOverlay's useGrabInteractive() && grab.count > 0)
    /// </summary>
    public static class GrabOverlayViewModelFactory
    {
        public static GrabOverlayViewModel Create(UiScreen screen, SlotItem grab)
        {
            var shown = UiScreenTraitsTable.Get(screen).Grab && !grab.IsEmpty;
            return shown ? GrabOverlayViewModel.Holding(grab.ItemId, grab.Count) : GrabOverlayViewModel.Hidden;
        }
    }
}
