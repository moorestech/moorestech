using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物の整理（Web inventory.sort）。整理はサーバーへ送るだけで、手元で断る条件は無い
    /// Sorts the inventory (Web inventory.sort); it only sends to the server and has no local refusal
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
