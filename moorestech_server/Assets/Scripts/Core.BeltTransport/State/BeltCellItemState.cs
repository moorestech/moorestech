namespace Core.BeltTransport
{
    public readonly struct BeltCellItemState
    {
        public readonly int CellId, Progress, EntryHeight;
        public readonly BeltDirection EntryDirection;
        public readonly BeltItem Item;
        public readonly bool IsBuffer;

        public BeltCellItemState(int cellId, int progress, BeltDirection entryDirection, int entryHeight, BeltItem item, bool isBuffer)
        {
            CellId = cellId; Progress = progress; EntryDirection = entryDirection;
            EntryHeight = entryHeight; Item = item; IsBuffer = isBuffer;
        }
    }
}
