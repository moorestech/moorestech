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
        readonly BeltConveyorSegment[] segments, merges, normal;
        readonly BeltBuffer[] buffers;
        static readonly Action<BeltBuffer> collectBuffer = buffer => buffer.Collect();
        static readonly Action<BeltConveyorSegment> reserveMerge = segment => segment.ResolveInput();
        static readonly Action<BeltBuffer> transferBuffer = buffer => buffer.Transfer();
        static readonly Action<BeltConveyorSegment> advanceNormal = segment => segment.AdvanceAndTransfer();

        public BeltSimulation(IEnumerable<BeltConveyorSegment> segments)
        {
            var all = new List<BeltConveyorSegment>();
            var mergeList = new List<BeltConveyorSegment>();
            var normalList = new List<BeltConveyorSegment>();
            var bufferList = new List<BeltBuffer>();
            // 段階ごとの対象を構築時に分類する。
            // Classify phase participants when the simulation is built.
            foreach (var segment in segments)
            {
                all.Add(segment);
                if (segment.Kind == BeltSegmentKind.Merge) mergeList.Add(segment);
                if (segment.Kind == BeltSegmentKind.Normal) normalList.Add(segment);
                else bufferList.Add(segment.Buffer);
            }
            this.segments = all.ToArray();
            merges = mergeList.ToArray();
            normal = normalList.ToArray();
            buffers = bufferList.ToArray();
        }

        public void Tick(bool parallel)
        {
            foreach (var segment in segments) segment.BeginTick(); // 0
            Run(buffers, collectBuffer, parallel);                 // 1
            Run(merges, reserveMerge, parallel);                   // 2
            Run(buffers, transferBuffer, parallel);                // 3
            Run(normal, advanceNormal, parallel);                 // 4
        }

        static void Run<T>(T[] values, Action<T> action, bool parallel)
        {
            if (parallel) Parallel.ForEach(values, action);
            else foreach (var value in values) action(value);
        }
    }
}
