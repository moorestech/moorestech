using Client.Ui.State.Commands;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロックの枠の操作（Web useBlockSlotGestures の 3 つ。ブロックの枠には進入の操作が無い）
    /// Block-slot commands (the three of Web useBlockSlotGestures; block slots have no enter gestures)
    /// </summary>
    public interface IBlockSlotCommands
    {
        UiCommandResult LeftDown(int blockSlot, bool shift);
        UiCommandResult RightDown(int blockSlot);
        UiCommandResult DoubleClick(int blockSlot);
    }
}
