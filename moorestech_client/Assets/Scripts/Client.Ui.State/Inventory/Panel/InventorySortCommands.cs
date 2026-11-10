using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物の整理（サーバーへ送るのみ）
    /// Sorts the inventory (only sends to the server)
    /// </summary>
    public sealed class InventorySortCommands : IInventorySortCommands
    {
        public const string CommandName = "inventory.sort";
        private readonly LocalPlayerInventoryController _controller;

        public InventorySortCommands(LocalPlayerInventoryController controller)
        {
            _controller = controller;
        }

        public UiCommandResult Sort()
        {
            _controller.SortInventory();
            return UiCommandAccepted.Instance;
        }
    }
}
