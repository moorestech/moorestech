using Game.Gear.Common;

namespace Game.Block.Blocks.BeltConveyor.Transport.Drive
{
    // 歯車で駆動されるsegment1本分。先頭マスの歯車と、基準RPMでのマスタ速度を持つ
    // One gear-driven segment: the head cell's gear and the master speed at the base RPM
    public readonly struct BeltGearDrivenSegment
    {
        public readonly int SegmentIndex;
        public readonly IGearEnergyTransformer Transformer;
        public readonly float BaseRpm;
        public readonly int BaseSpeed;

        public BeltGearDrivenSegment(int segmentIndex, IGearEnergyTransformer transformer, float baseRpm, int baseSpeed)
        {
            SegmentIndex = segmentIndex;
            Transformer = transformer;
            BaseRpm = baseRpm;
            BaseSpeed = baseSpeed;
        }
    }
}
