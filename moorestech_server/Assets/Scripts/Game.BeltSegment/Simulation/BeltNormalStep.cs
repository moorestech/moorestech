namespace Game.BeltSegment
{
    internal readonly struct BeltNormalStep
    {
        readonly BeltConveyorSegment segment;
        readonly BeltNormalTransfer transfer;

        internal BeltNormalStep(BeltConveyorSegment segment, BeltNormalTransfer transfer)
        {
            this.segment = segment;
            this.transfer = transfer;
        }

        internal void Advance() => segment.AdvanceAndTransfer(transfer);
    }
}
