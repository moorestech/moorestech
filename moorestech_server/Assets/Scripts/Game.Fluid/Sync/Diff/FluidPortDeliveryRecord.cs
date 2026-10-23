namespace Game.Fluid.Sync.Diff
{
    // 流体tickで境界ポートが機械へ実際に渡した量(単位)。複製はこの量だけを機械が受け取ったものとして再生する
    // The units a boundary port actually handed to its machine during the fluid tick; the replica replays exactly this as accepted by the machine
    public readonly struct FluidPortDeliveryRecord
    {
        public readonly int PortIndex;
        public readonly long DeliveredUnits;

        public FluidPortDeliveryRecord(int portIndex, long deliveredUnits)
        {
            PortIndex = portIndex;
            DeliveredUnits = deliveredUnits;
        }
    }
}
