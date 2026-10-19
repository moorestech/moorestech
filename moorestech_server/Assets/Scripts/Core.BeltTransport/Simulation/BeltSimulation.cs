using System.Collections.Generic;

namespace Core.BeltTransport
{
    // 0:速度確定、1:buffer回収、2:合流予約、3:buffer搬出、4:空き記録・通常前進・搬送反映を逐次実行する
    // 各段階の全件完了を待って次へ進む。外部供給・速度変更・配線変更はTickの外で行う
    // 接続やsegmentの構成を変えたら、この更新対象一覧を作り直す
    // Runs 0: fix speed, 1: buffer collect, 2: merge reservation, 3: buffer output, 4: offer capture, normal advance, transfer apply, sequentially
    // Each stage finishes for all targets before the next. Supply, speed and wiring changes happen outside Tick
    // Rebuild this update list whenever connections or segments change
    // 段階は Stages として公開する。段階内の要素は独立で、段数は網の大きさによらず一定(ADR 0084 §3。テストが順序入れ替え・並列実行・段数で確かめる)
    // The stages are exposed as Stages; elements within a stage are independent and the stage count does not grow with the network (ADR 0084 §3; tests reorder, parallelize and count them)
    public sealed class BeltSimulation
    {
        public IReadOnlyList<IBeltTickStage> Stages => _stages;
        private readonly IBeltTickStage[] _stages;

        public BeltSimulation(IEnumerable<BeltConveyorSegment> segments)
        {
            // 合流・通常に分類し、合流・分岐のbufferを集める
            // Classify into merge and normal, collecting the buffers of merges and branches
            var all = new List<BeltConveyorSegment>();
            var mergeList = new List<BeltMergeSegment>();
            var normalList = new List<BeltNormalSegment>();
            var bufferList = new List<BeltBuffer>();
            foreach (var segment in segments)
            {
                all.Add(segment);
                if (segment is BeltMergeSegment merge) mergeList.Add(merge);
                if (segment is BeltNormalSegment normal) normalList.Add(normal);
                else if (segment is BeltBufferedSegment buffered) bufferList.Add(buffered.Buffer);
            }

            // 通常→通常の接続はここでキャッシュし、tick中に探索しない
            // Cache normal-to-normal connections here so a tick never searches them
            var normals = normalList.ToArray();
            var buffers = bufferList.ToArray();
            var transfers = BeltSegmentTransfer.Cache(normals);
            _stages = new IBeltTickStage[]
            {
                new BeltBeginTickStage(all.ToArray()),
                new BeltBufferCollectStage(buffers),
                new BeltMergeResolveStage(mergeList.ToArray()),
                new BeltBufferTransferStage(buffers),
                new BeltOfferCaptureStage(transfers),
                new BeltNormalAdvanceStage(normals),
                new BeltTransferApplyStage(transfers),
            };
        }

        public void Tick()
        {
            foreach (var stage in _stages)
            {
                for (var i = 0; i < stage.Count; i++) stage.Run(i);
            }
        }
    }
}
