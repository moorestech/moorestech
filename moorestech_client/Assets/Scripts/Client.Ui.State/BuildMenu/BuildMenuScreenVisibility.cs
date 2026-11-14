using Client.Ui.State.Routing;

namespace Client.Ui.State.BuildMenu
{
    /// <summary>
    /// ビルドメニューの画面を出すか（前例 BlockInventoryScreenVisibility。A-248）
    /// Whether to show the build-menu screen (precedent BlockInventoryScreenVisibility; A-248)
    /// </summary>
    public static class BuildMenuScreenVisibility
    {
        public static bool IsShown(UiRoutingSnapshot snapshot)
        {
            return snapshot.Screen == UiScreen.BuildMenu;
        }
    }
}
