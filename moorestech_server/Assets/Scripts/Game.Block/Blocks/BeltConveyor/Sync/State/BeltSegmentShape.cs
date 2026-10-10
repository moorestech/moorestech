using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 全量に載せるsegment1本の形。構成(BeltSegmentLayout)からblockの個体・機械への参照を落としたもので、クライアントはこれだけで組み立てる
    // The shape of one segment in the full state: the layout with block instances and machine references stripped; the client assembles from this alone
    public sealed class BeltSegmentShape
    {
        public readonly BeltSegmentKind Kind;
        // 機械・分岐bufferと合流の間のマスを持たないsegment。表示しない
        // A cell-less segment between a machine or branch buffer and a merge; never displayed
        public readonly bool IsInternal;
        // 搬送順に並んだマス。内部segmentは空
        // Cells in transport order; empty for an internal segment
        public readonly BeltCellShape[] Cells;
        public readonly int Speed;
        // 末尾マスの正面方向。内部segmentは合流へ向かう方向
        // Forward direction of the last cell; for an internal segment, the direction toward the merge
        public readonly BeltDirection Forward;
        public readonly BeltLinkShape[] Inputs;
        public readonly BeltLinkShape[] Outputs;

        public int Capacity => IsInternal ? 1 : Cells.Length;

        public BeltSegmentShape(BeltSegmentKind kind, bool isInternal, BeltCellShape[] cells, int speed, BeltDirection forward, BeltLinkShape[] inputs, BeltLinkShape[] outputs)
        {
            Kind = kind;
            IsInternal = isInternal;
            Cells = cells;
            Speed = speed;
            Forward = forward;
            Inputs = inputs;
            Outputs = outputs;
        }

        public static BeltSegmentShape FromLayout(BeltSegmentLayout layout)
        {
            var cells = new BeltCellShape[layout.Cells.Length];
            for (var i = 0; i < cells.Length; i++) cells[i] = new BeltCellShape(layout.Cells[i].Position, layout.Cells[i].Forward);
            return new BeltSegmentShape(layout.Kind, layout.IsInternal, cells, layout.Speed, layout.Forward, FromLinks(layout.Inputs), FromLinks(layout.Outputs));
        }

        private static BeltLinkShape[] FromLinks(BeltSegmentLayoutLink[] links)
        {
            // 構成のMachine(-1)と全量のMachine(-1)は同じ値。相手番号はそのまま写す
            // The layout's Machine (-1) and the full state's Machine (-1) are the same value, so partner numbers copy over as they are
            var shapes = new BeltLinkShape[links.Length];
            for (var i = 0; i < links.Length; i++) shapes[i] = new BeltLinkShape(links[i].Direction, links[i].EntryDirection, links[i].PartnerSegmentIndex);
            return shapes;
        }
    }
}
