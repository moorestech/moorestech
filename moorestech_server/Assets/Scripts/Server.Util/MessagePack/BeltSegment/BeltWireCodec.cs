using System;
using System.Linq;
using Game.BeltSegment;
namespace Server.Util.MessagePack.BeltSegment
{
    public static class BeltWireCodec
    {
        public static BeltWorldSnapshot Decode(BeltWorldSnapshotMessagePack value)
        {
            // 全体検証後だけモデルを生成し、途中状態の所有を防ぐ。
            // Construct models only after whole-payload validation to prevent partial ownership.
            BeltSnapshotValidation.Validate(value);
            var simulation = new BeltReplaySnapshot(value.Segments.Select(x => x.Decode()).ToArray(),
                value.Links.Select(x => x.Decode()).ToArray(), value.Inputs.Select(x => x.Decode()).ToArray(),
                value.Outputs.Select(x => x.Decode()).ToArray());
            return new(new(value.Tick, value.Sequence), value.Generation, simulation, value.Routes.Select(x => x.Decode()).ToArray());
        }
        public static BeltWorldFrame Decode(BeltWorldFrameMessagePack value)
        {
            BeltWireValidation.Require(value != null && value.Complete, "Missing frame.");
            BeltWireValidation.Require(value.Tick > value.PreviousTick && value.Tick - value.PreviousTick == 1 && value.Sequence == 1,
                "A frame must advance one physical tick and reset its sequence to one.");
            BeltTickValidation.Validate(value.Replay);
            return new(new(value.PreviousTick, value.PreviousSequence), new(value.Tick, value.Sequence),
                value.Generation, value.PreviousHash, value.Replay.Decode());
        }
        public static void ValidateFrame(BeltWorldFrame frame, BeltReplaySnapshot topology)
        {
            // 世代内IDを適用前に全件確認する。Coreの途中変異より前の外部境界。
            // Check every generation-local ID at the external boundary before any Core mutation.
            foreach (var change in frame.Replay.SpeedChanges) BeltWireValidation.Index(change.SegmentId, topology.Segments.Length);
            foreach (int id in frame.Replay.ReadyInputs) BeltWireValidation.Index(id, topology.Inputs.Length);
            foreach (int id in frame.Replay.SuccessfulOutputs) BeltWireValidation.Index(id, topology.Outputs.Length);
            var targets = new System.Collections.Generic.HashSet<int>();
            foreach (var entry in frame.Replay.Insertions)
            {
                BeltWireValidation.Index(entry.InputId, topology.Inputs.Length);
                BeltWireValidation.Require(targets.Add(topology.Inputs[entry.InputId].TargetSegmentId), "Multiple insertions into one segment.");
            }
        }
    }
}
