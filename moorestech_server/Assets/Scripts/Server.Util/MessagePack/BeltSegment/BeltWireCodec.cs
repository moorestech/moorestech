using System;
using System.Linq;
using Game.BeltSegment;
namespace Server.Util.MessagePack.BeltSegment
{
    public static class BeltWireCodec
    {
        public static BeltWorldSnapshot Decode(BeltWorldSnapshotMessagePack value)
        {
            if (!TryDecode(value, out var snapshot, out var reason)) throw new ArgumentException(reason);
            return snapshot;
        }
        public static BeltWorldFrame Decode(BeltWorldFrameMessagePack value)
        {
            if (!TryDecode(value, out var frame, out var reason)) throw new ArgumentException(reason);
            return frame;
        }
        public static bool TryDecode(BeltWorldSnapshotMessagePack value, out BeltWorldSnapshot snapshot, out string reason)
        {
            snapshot = null;
            reason = BeltSnapshotValidation.Validate(value);
            if (reason != null) return false;
            // 全入力の検証が終わるまでモデルを公開しない。
            // Publish no model until validation of the entire external payload succeeds.
            var simulation = new BeltReplaySnapshot(value.Segments.Select(x => x.Decode()).ToArray(),
                value.Links.Select(x => x.Decode()).ToArray(), value.Inputs.Select(x => x.Decode()).ToArray(),
                value.Outputs.Select(x => x.Decode()).ToArray());
            snapshot = new(new(value.Tick, value.Sequence), value.Generation, simulation, value.Routes.Select(x => x.Decode()).ToArray());
            return true;
        }
        public static bool TryDecode(BeltWorldFrameMessagePack value, out BeltWorldFrame frame, out string reason)
        {
            frame = null;
            reason = value == null || !value.Complete ? "Missing frame." :
                !(value.PreviousTick < value.Tick && value.Tick - value.PreviousTick == 1 && value.Sequence == 1)
                    ? "A frame must advance one physical tick and reset its sequence to one." : BeltTickValidation.Validate(value.Replay);
            if (reason != null) return false;
            frame = new(new(value.PreviousTick, value.PreviousSequence), new(value.Tick, value.Sequence),
                value.Generation, value.PreviousHash, value.Replay.Decode());
            return true;
        }
        public static void ValidateFrame(BeltWorldFrame frame, BeltReplaySnapshot topology)
        {
            if (!TryValidateFrame(frame, topology, out var reason)) throw new ArgumentException(reason);
        }
        public static bool TryValidateFrame(BeltWorldFrame frame, BeltReplaySnapshot topology, out string reason)
        {
            // 世代内IDと挿入先の重複をCPU/GPUの変更前に拒否する。
            // Reject invalid generation IDs and repeated insertion targets before CPU/GPU mutation.
            reason = "ID outside generation.";
            foreach (var change in frame.Replay.SpeedChanges)
                if (BeltWireValidation.Index(change.SegmentId, topology.Segments.Length) != null) return false;
            foreach (int id in frame.Replay.ReadyInputs)
                if (BeltWireValidation.Index(id, topology.Inputs.Length) != null) return false;
            foreach (int id in frame.Replay.SuccessfulOutputs)
                if (BeltWireValidation.Index(id, topology.Outputs.Length) != null) return false;
            foreach (var entry in frame.Replay.Insertions)
                if (BeltWireValidation.Index(entry.InputId, topology.Inputs.Length) != null) return false;
            for (int i = 0; i < frame.Replay.Insertions.Length; i++)
                for (int j = 0; j < i; j++)
                    if (topology.Inputs[frame.Replay.Insertions[i].InputId].TargetSegmentId == topology.Inputs[frame.Replay.Insertions[j].InputId].TargetSegmentId)
                    { reason = "Multiple insertions into one segment."; return false; }
            reason = null;
            return true;
        }
    }
}
