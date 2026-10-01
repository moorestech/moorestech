namespace Core.BeltTransport
{
    public readonly struct BeltOutputResult
    {
        public readonly int SourceCellId, TargetId, Stage, Offer;
        public readonly BeltDirection Direction;
        public readonly bool Succeeded;
        public readonly BeltItem Item;
        public BeltOutputResult(int sourceCellId, int targetId, int stage, BeltDirection direction, int offer, bool succeeded, BeltItem item)
        {
            SourceCellId = sourceCellId; TargetId = targetId; Stage = stage; Direction = direction;
            Offer = offer; Succeeded = succeeded; Item = item;
        }
    }
}
