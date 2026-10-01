// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation.
using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal sealed class BeltSimulation
    {
        private readonly BeltConveyorSegment[] merges, normal;
        private readonly BeltSegmentTransfer[] transfers;
        private readonly BeltBuffer[] buffers;

        public BeltSimulation(IEnumerable<BeltConveyorSegment> segments)
        {
            var mergeList = new List<BeltConveyorSegment>();
            var normalList = new List<BeltConveyorSegment>();
            var bufferList = new List<BeltBuffer>();

            // 呼び出し側の確定順序を保った更新対象を構築する。
            // Preserve caller-defined order when classifying update targets.
            foreach (var segment in segments)
            {
                if (segment.Kind == BeltSegmentKind.Merge) mergeList.Add(segment);
                if (segment.Kind == BeltSegmentKind.Normal) normalList.Add(segment);
                else bufferList.Add(segment.Buffer);
            }
            merges = mergeList.ToArray();
            normal = normalList.ToArray();
            transfers = BeltSegmentTransfer.Cache(normal);
            buffers = bufferList.ToArray();
        }

        public void Tick()
        {
            // 段階1〜2を全件ずつ完了して予約を固定する。
            // Complete stages one and two before using reservations.
            foreach (var buffer in buffers) buffer.Collect();
            foreach (var merge in merges) merge.ResolveInput();

            // 段階3の搬入は段階4の前進にも含める。
            // Inputs from stage three participate in stage four movement.
            foreach (var buffer in buffers) buffer.Transfer();
            foreach (var transfer in transfers) transfer.CaptureOffer();
            foreach (var segment in normal) segment.AdvanceAndTransfer();
            foreach (var transfer in transfers) transfer.Apply();
        }
    }
}
