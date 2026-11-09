using Client.Ui.State.Routing;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// ブロックインベントリの画面を出すか（画面ルータが SubInventory で、パネルが閉でない。閉は Web の return null）
    /// Whether to show the block-inventory screen (router on SubInventory and the panel not closed; closed is the Web return null)
    /// </summary>
    public static class BlockInventoryScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot, BlockInventoryPanelState state)
        {
            return snapshot.Screen == UiScreen.SubInventory && !(state is ClosedBlockInventoryPanel);
        }
    }
}
