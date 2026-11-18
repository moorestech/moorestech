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
            return IsOnScreen(snapshot) && !(state is ClosedBlockInventoryPanel);
        }

        // 画面ルータの側の条件だけ。Presenter はこれが真の間だけパネルを購読する
        // The router-side condition only; the presenter follows the panel only while this holds
        public static bool IsOnScreen(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.SubInventory;
        }
    }
}
