using Client.Ui.Content;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 区画が使うブロックの枠の並びを、共通の依存つきで作る
    /// Creates the block slot grids sections use, wired with the shared dependencies
    /// </summary>
    public sealed class BlockItemGridFactory
    {
        private readonly IBlockSlotCommands _commands;
        private readonly GameContentPresentation _content;

        public BlockItemGridFactory(IBlockSlotCommands commands, GameContentPresentation content)
        {
            _commands = commands;
            _content = content;
        }

        public BlockItemGrid Create(string testId)
        {
            return new BlockItemGrid(testId, _commands, _content);
        }
    }
}
