namespace Core.BeltTransport
{
    public readonly struct BeltNetworkConnection
    {
        public readonly int SourceId, TargetId, EntryHeight;
        public readonly bool SourceIsBelt, TargetIsBelt;
        public readonly BeltDirection Direction;

        public BeltNetworkConnection(int sourceId, int targetId, bool sourceIsBelt, bool targetIsBelt, BeltDirection direction, int entryHeight)
        {
            SourceId = sourceId; TargetId = targetId; EntryHeight = entryHeight;
            SourceIsBelt = sourceIsBelt; TargetIsBelt = targetIsBelt; Direction = direction;
        }
    }
}
