namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// ブロックインベントリのパネルの操作（閉じる＝Web の ui_state.request gameScreen。結果は実装がトーストへ渡す）
    /// Block-inventory panel commands (close = the Web ui_state.request gameScreen; the implementation hands results to the toaster)
    /// </summary>
    public interface IBlockInventoryPanelCommands
    {
        void Close();
    }
}
