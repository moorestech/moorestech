namespace Game.Fluid.Sync.Diff
{
    // 流体tickで境界ポートが機械へ実際に渡した量。複製はこの量だけを機械が受け取ったものとして再生する
    // The amount a boundary port actually handed to its machine during the fluid tick; the replica replays exactly this as accepted by the machine
    public readonly struct FluidPortDeliveryRecord
    {
        public readonly int PortIndex;
        public readonly double DeliveredAmount;

        public FluidPortDeliveryRecord(int portIndex, double deliveredAmount)
        {
            PortIndex = portIndex;
            DeliveredAmount = deliveredAmount;
        }
    }
}
