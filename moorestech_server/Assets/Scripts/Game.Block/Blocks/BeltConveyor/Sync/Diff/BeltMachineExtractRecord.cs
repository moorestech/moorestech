using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Sync.Diff
{
    // 差分2・3種め: 通常segment→機械、buffer→機械の搬出成功。搬出したsegment番号と搬出方向(通常は唯一の出力、bufferは成功した方向)
    // Diff kinds 2 and 3: a successful handoff from a normal segment or a buffer into a machine; the emitting segment number and the output direction (the only output for a normal segment, the succeeded direction for a buffer)
    public readonly struct BeltMachineExtractRecord
    {
        public readonly int SegmentIndex;
        public readonly BeltDirection OutputDirection;

        public BeltMachineExtractRecord(int segmentIndex, BeltDirection outputDirection)
        {
            SegmentIndex = segmentIndex;
            OutputDirection = outputDirection;
        }
    }
}
