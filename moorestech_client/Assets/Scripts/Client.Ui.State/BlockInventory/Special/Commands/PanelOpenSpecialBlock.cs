using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 開いたブロックの識別を S4-alpha のパネルの源から読む（詳細の Model・Shift の寄与と同じ識別。D-26）
    /// Reads the open block's identity from S4-alpha's panel source (the same identity as the detail model and the Shift contributor. D-26)
    /// </summary>
    public sealed class PanelOpenSpecialBlock : IOpenSpecialBlock
    {
        private readonly IBlockInventoryPanelSource _panels;

        public PanelOpenSpecialBlock(IBlockInventoryPanelSource panels)
        {
            _panels = panels;
        }

        public bool TryGet(out OpenSpecialBlock block)
        {
            block = default;
            if (_panels.Current.Value is not OpenBlockInventoryPanel { Error: BlockInventoryPanelError.None } open) return false;
            block = new OpenSpecialBlock(open.Position, open.BlockType, open.BlockInstanceId);
            return true;
        }
    }
}
