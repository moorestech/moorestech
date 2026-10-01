using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltCellItemMessagePack
    {
        [Key(0)] public int CellId { get; }
        [Key(1)] public int Progress { get; }
        [Key(2)] public BeltDirection EntryDirection { get; }
        [Key(3)] public int EntryHeight { get; }
        [Key(4)] public BeltItemMessagePack Item { get; }
        [Key(5)] public bool IsBuffer { get; }
        [SerializationConstructor]
        public BeltCellItemMessagePack(int cellId, int progress, BeltDirection entryDirection, int entryHeight, BeltItemMessagePack item, bool isBuffer)
        {
            CellId = cellId;
            Progress = progress;
            EntryDirection = entryDirection;
            EntryHeight = entryHeight;
            Item = item;
            IsBuffer = isBuffer;
        }
        public BeltCellItemMessagePack(BeltCellItemState value)
        {
            CellId = value.CellId; Progress = value.Progress; EntryDirection = value.EntryDirection;
            EntryHeight = value.EntryHeight; Item = new(value.Item); IsBuffer = value.IsBuffer;
        }
        internal BeltCellItemState ToCore() => new(CellId, Progress, EntryDirection, EntryHeight, Item.ToCore(), IsBuffer);
    }
}
