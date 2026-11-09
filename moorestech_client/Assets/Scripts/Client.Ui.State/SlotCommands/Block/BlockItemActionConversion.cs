using Client.Ui.State.ItemMove;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// L0-s4a の計画 1 件をブロック側の行為へ写す境目（PlayerItemActionConversion の対）。Block* の 3 種でなければ false
    /// Boundary converting one L0-s4a plan into a block-side action (counterpart of PlayerItemActionConversion); false unless one of the three Block* kinds
    /// </summary>
    public static class BlockItemActionConversion
    {
        public static bool TryConvert(PlannedItemAction action, out BlockItemAction blockAction)
        {
            blockAction = action.Kind switch
            {
                PlannedItemActionKind.BlockInventoryMoveItem => new BlockMoveItemAction(action.From, action.To, action.Count),
                PlannedItemActionKind.BlockInventorySplit => new BlockSplitToGrabAction(action.From.Slot),
                PlannedItemActionKind.BlockInventoryCollect => new BlockCollectAction(action.From.Slot),
                _ => null,
            };
            return blockAction != null;
        }
    }
}
