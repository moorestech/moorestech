using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface.Extension;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor.Transport.Drive
{
    // 歯車ベルコンのsegmentへ、先頭マスの歯車の回転数から決めた速度を搬送tickの前に与える
    // Gives each gear-belt segment the speed derived from its head cell's gear rotation before the transport tick
    // 同じ種類の歯車ベルコンがアイテムで繋がる並びは歯車も繋がるので、先頭マスの回転数がsegment全体を代表する
    // Same-kind gear belts linked by items are also gear-linked, so the head cell's rotation stands for the whole segment
    public sealed class BeltGearDrive
    {
        private readonly BeltGearDrivenSegment[] _drivenSegments;

        private BeltGearDrive(BeltGearDrivenSegment[] drivenSegments)
        {
            _drivenSegments = drivenSegments;
        }

        public static BeltGearDrive Build(BeltSegmentLayout[] layouts)
        {
            var driven = new List<BeltGearDrivenSegment>();
            foreach (var layout in layouts)
            {
                // 内部segmentは機械から合流への受け渡し専用で速度固定
                // Internal segments only hand machine pushes to a merge and keep a fixed speed
                if (layout.IsInternal) continue;
                var head = layout.Cells[0];
                if (!head.IsGearDriven) continue;
                var transformer = head.Block.GetComponent<IGearEnergyTransformer>();
                var consumption = ((IGearConsumptionParam)head.Block.BlockMasterElement.BlockParam).GearConsumption;
                driven.Add(new BeltGearDrivenSegment(layout.Index, transformer, (float)consumption.BaseRpm, layout.Speed));
            }
            return new BeltGearDrive(driven.ToArray());
        }

        // 速度が変わったsegmentだけ書き換え、差分に載せる
        // Rewrite only the segments whose speed changed and put them on the diff
        public void ApplySpeeds(BeltConveyorSegment[] segments, BeltTransportDiffRecorder diffRecorder)
        {
            foreach (var driven in _drivenSegments)
            {
                var speed = BeltGearDriveSpeed.Resolve(driven.BaseSpeed, driven.Transformer.CurrentRpm.AsPrimitive(), driven.BaseRpm);
                var segment = segments[driven.SegmentIndex];
                if (segment.Speed == speed) continue;
                segment.SetSpeed(speed);
                diffRecorder.RecordSpeed(driven.SegmentIndex, speed);
            }
        }
    }
}
