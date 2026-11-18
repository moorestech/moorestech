using Client.Ui.State.BlockInventory.Logic.Panel;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 拡張口 (2): blockType ごとの題の操作（ロケットの発射。ブロック・エラー無しのときだけ出す）
    /// Extension port (2): a title action per blockType (the rocket launch; shown only for blocks without errors)
    /// </summary>
    public interface IBlockInventoryTitleActionFactory
    {
        BlockInventoryTitleActionKind Kind { get; }
        IBlockInventoryPart Create();
    }
}
