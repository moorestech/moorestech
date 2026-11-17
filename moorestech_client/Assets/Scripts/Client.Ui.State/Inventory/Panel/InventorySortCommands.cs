using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物の整理（サーバーへ送り、受理をトースト口へ渡す）
    /// Sorts the inventory (sends to the server and hands the acceptance to the toaster)
    /// </summary>
    public sealed class InventorySortCommands : IInventorySortCommands
    {
        public const string CommandName = "inventory.sort";
        private readonly LocalPlayerInventoryController _controller;
        private readonly IUiCommandResultToaster _toaster;

        public InventorySortCommands(LocalPlayerInventoryController controller, IUiCommandResultToaster toaster)
        {
            _controller = controller;
            _toaster = toaster;
        }

        public void Sort()
        {
            _controller.SortInventory();
            _toaster.Report(CommandName, UiCommandAccepted.Instance);
        }
    }
}
