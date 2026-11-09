using Client.Ui.State.BlockInventory.Logic.Panel;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// 拡張口 (1): blockType ごとの専用本文（Kind は L0-s4b の BlockComponentRegistry が blockType から引く）
    /// Extension port (1): a dedicated body per blockType (L0-s4b BlockComponentRegistry resolves the Kind from blockType)
    /// </summary>
    public interface IBlockInventoryBodyFactory
    {
        BlockInventoryBodyKind Kind { get; }
        IBlockInventoryPart Create(BlockInventoryBodyMount mount);
    }
}
