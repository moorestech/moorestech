using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Game.BeltSegment
{
    /// <summary>
    /// 0:速度確定、1:buffer回収、2:合流予約、3:buffer搬出、4:通常segment前進。
    /// 各段階の全件完了を待って次へ進む。外部供給・速度変更・配線変更はTickの外で行う。
    /// 接続やsegmentの構成を変えたら、この更新対象一覧を作り直す。
    /// </summary>
    public sealed class BeltSimulation
    {
        readonly BeltConveyorSegment[] segments, merges;
        readonly BeltNormalStep[] normal;
        readonly BeltNormalTransfer[] normalTransfers;
        readonly BeltBuffer[] buffers;
        static readonly Action<BeltBuffer> collectBuffer = buffer => buffer.Collect();
        static readonly Action<BeltConveyorSegment> reserveMerge = segment => segment.ResolveInput();
        static readonly Action<BeltBuffer> transferBuffer = buffer => buffer.Transfer();
        static readonly Action<BeltNormalStep> advanceNormal = step => step.Advance();

        public BeltSimulation(IEnumerable<BeltConveyorSegment> segments)
        {
            var all = new List<BeltConveyorSegment>();
            var mergeList = new List<BeltConveyorSegment>();
            var normalList = new List<BeltNormalStep>();
            var transferList = new List<BeltNormalTransfer>();
            var bufferList = new List<BeltBuffer>();
            foreach (var segment in segments)
            {
                all.Add(segment);
                if (segment.Kind == BeltSegmentKind.Merge) mergeList.Add(segment);
                // 固定配線から通常列間の搬送を抽出する。
                // Extract transfers between Normal queues from the fixed wiring.
                if (segment.Kind == BeltSegmentKind.Normal)
                {
                    BeltNormalTransfer transfer = null;
                    if (segment.Output is BeltConveyorSegment target && target.Kind == BeltSegmentKind.Normal)
                    {
                        transfer = new BeltNormalTransfer(target, BeltDirections.Opposite(segment.OutputDirection));
                        transferList.Add(transfer);
                    }
                    normalList.Add(new BeltNormalStep(segment, transfer));
                }
                else bufferList.Add(segment.Buffer);
            }
            this.segments = all.ToArray();
            merges = mergeList.ToArray();
            normal = normalList.ToArray();
            normalTransfers = transferList.ToArray();
            buffers = bufferList.ToArray();
        }

        public void Tick(bool parallel)
        {
            // 前tickの参照状態を全件固定してから既存段階を始める。
            // Capture all prior-tick reads before starting the existing phases.
            foreach (var transfer in normalTransfers) transfer.CaptureAvailableSpace();
            foreach (var segment in segments) segment.BeginTick(); // 0
            Run(buffers, collectBuffer, parallel);                 // 1
            Run(merges, reserveMerge, parallel);                   // 2
            Run(buffers, transferBuffer, parallel);                // 3
            Run(normal, advanceNormal, parallel);                 // 4
            // 全通常列の前進後に入力を確定し、二重前進を防ぐ。
            // Commit after every Normal queue advances to prevent advancing twice.
            foreach (var transfer in normalTransfers) transfer.Commit();
        }

        static void Run<T>(T[] values, Action<T> action, bool parallel)
        {
            if (parallel) Parallel.ForEach(values, action);
            else foreach (var value in values) action(value);
        }
    }
}
