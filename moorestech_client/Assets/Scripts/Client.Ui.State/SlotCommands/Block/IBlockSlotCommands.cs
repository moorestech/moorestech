namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロックの枠の操作（Web useBlockSlotGestures の 3 つ。結果は実装がトーストへ渡す）
    /// Block-slot commands (the three of Web useBlockSlotGestures; the implementation hands results to the toaster)
    /// </summary>
    public interface IBlockSlotCommands
    {
        void LeftDown(int blockSlot, bool shift);
        void RightDown(int blockSlot);
        void DoubleClick(int blockSlot);
    }
}
