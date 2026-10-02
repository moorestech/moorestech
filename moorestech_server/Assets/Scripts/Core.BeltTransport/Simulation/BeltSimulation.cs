using System.Collections.Generic;

namespace Core.BeltTransport
{
    // 0:速度確定、1:buffer回収、2:合流予約、3:buffer搬出、4:空き記録・通常前進・搬送反映を逐次実行する
    // 各段階の全件完了を待って次へ進む。外部供給・速度変更・配線変更はTickの外で行う
    // 接続やsegmentの構成を変えたら、この更新対象一覧を作り直す
    // Runs 0: fix speed, 1: buffer collect, 2: merge reservation, 3: buffer output, 4: offer capture, normal advance, transfer apply, sequentially
    // Each stage finishes for all targets before the next. Supply, speed and wiring changes happen outside Tick
    // Rebuild this update list whenever connections or segments change
    public sealed class BeltSimulation
    {
        private readonly BeltConveyorSegment[] _segments, _merges, _normal;
        private readonly BeltSegmentTransfer[] _transfers;
        private readonly BeltBuffer[] _buffers;

        public BeltSimulation(IEnumerable<BeltConveyorSegment> segments)
        {
            // 合流・通常に分類し、合流・分岐のbufferを集める
            // Classify into merge and normal, collecting the buffers of merges and branches
            var all = new List<BeltConveyorSegment>();
            var mergeList = new List<BeltConveyorSegment>();
            var normalList = new List<BeltConveyorSegment>();
            var bufferList = new List<BeltBuffer>();
            foreach (var segment in segments)
            {
                all.Add(segment);
                if (segment.Kind == BeltSegmentKind.Merge) mergeList.Add(segment);
                if (segment.Kind == BeltSegmentKind.Normal) normalList.Add(segment);
                else bufferList.Add(segment.Buffer);
            }

            // 通常→通常の接続はここでキャッシュし、tick中に探索しない
            // Cache normal-to-normal connections here so a tick never searches them
            _segments = all.ToArray();
            _merges = mergeList.ToArray();
            _normal = normalList.ToArray();
            _transfers = BeltSegmentTransfer.Cache(_normal);
            _buffers = bufferList.ToArray();
        }

        public void Tick()
        {
            // 段階0〜3: 速度固定、buffer回収、合流予約、buffer搬出
            // Stages 0-3: fix speed, buffer collect, merge reservation, buffer output
            foreach (var segment in _segments) segment.BeginTick();
            foreach (var buffer in _buffers) buffer.Collect();
            foreach (var merge in _merges) merge.ResolveInput();
            foreach (var buffer in _buffers) buffer.Transfer();

            // 段階4: 全接続の空き記録 → 全通常segmentの前進・搬出 → 成立した搬送の反映
            // Stage 4: capture every offer, advance all normal segments, then apply settled transfers
            foreach (var transfer in _transfers) transfer.CaptureOffer();
            foreach (var segment in _normal) segment.AdvanceAndTransfer();
            foreach (var transfer in _transfers) transfer.Apply();
        }
    }
}
