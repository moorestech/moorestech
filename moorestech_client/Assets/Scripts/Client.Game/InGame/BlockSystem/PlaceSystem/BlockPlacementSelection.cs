using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    /// ビルドメニューで選択中の設置対象ブロック
    /// The block currently selected in the build menu for placement
    /// </summary>
    public class BlockPlacementSelection
    {
        public BlockId? SelectedBlockId { get; private set; }

        public void SetSelectedBlock(BlockId blockId)
        {
            SelectedBlockId = blockId;
        }

        public void ClearSelection()
        {
            SelectedBlockId = null;
        }
    }
}
