namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 本文を作るときの条件（Web の fillsPanelHeight。大型パネルだけ真）
    /// Conditions when creating a body (Web fillsPanelHeight; true only on the large panel)
    /// </summary>
    public sealed class BlockInventoryBodyMount
    {
        public bool FillsPanelHeight { get; private set; }

        public BlockInventoryBodyMount(bool fillsPanelHeight)
        {
            FillsPanelHeight = fillsPanelHeight;
        }
    }
}
