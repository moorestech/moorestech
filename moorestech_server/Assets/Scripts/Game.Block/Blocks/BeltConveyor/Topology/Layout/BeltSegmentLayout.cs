using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Topology.Layout
{
    // Coreのsegment1本の生成に必要な構成。マスの並び・種類・速度と、他segment・機械への接続を持つ
    // The configuration needed to create one Core segment: its cells, kind, speed and links to other segments and machines
    public sealed class BeltSegmentLayout
    {
        // 全量送信の並び順をそのまま番号にする。マスを持つsegmentは先頭マスの座標順、内部segmentは合流の直後に入力方向順
        // The full-state order doubles as the index: cell-bearing segments by head cell position, internal ones right after their merge in input direction order
        public readonly int Index;
        public readonly BeltSegmentKind Kind;
        // 機械・分岐bufferと合流の間に置く、マスを持たない1マス分の通常segment。表示しない
        // A cell-less one-cell normal segment placed between a machine or branch buffer and a merge; never displayed
        public readonly bool IsInternal;
        // 搬送順に並んだマス。内部segmentは空
        // Cells in transport order; empty for an internal segment
        public readonly BeltTopologyCell[] Cells;
        public readonly int Speed;
        // 末尾マスの正面方向。内部segmentは合流へ向かう方向
        // Forward direction of the last cell; for an internal segment, the direction toward the merge
        public readonly BeltDirection Forward;
        // 合流は方向順に最大3本、それ以外は最大1本
        // Up to three in direction order for a merge, at most one otherwise
        public readonly BeltSegmentLayoutLink[] Inputs;
        // 分岐は方向順に最大3本、それ以外は最大1本
        // Up to three in direction order for a branch, at most one otherwise
        public readonly BeltSegmentLayoutLink[] Outputs;

        public int Capacity => IsInternal ? 1 : Cells.Length;

        public BeltSegmentLayout(int index, BeltSegmentKind kind, bool isInternal, BeltTopologyCell[] cells, int speed, BeltDirection forward,
            BeltSegmentLayoutLink[] inputs, BeltSegmentLayoutLink[] outputs)
        {
            Index = index;
            Kind = kind;
            IsInternal = isInternal;
            Cells = cells;
            Speed = speed;
            Forward = forward;
            Inputs = inputs;
            Outputs = outputs;
        }
    }
}
