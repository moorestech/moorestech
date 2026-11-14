using Client.Ui.State.Commands;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// ブロックインベントリのパネルの操作（閉じる＝Web の ui_state.request gameScreen）
    /// Block-inventory panel commands (close = the Web ui_state.request gameScreen)
    /// </summary>
    public interface IBlockInventoryPanelCommands
    {
        UiCommandResult Close();
    }
}
