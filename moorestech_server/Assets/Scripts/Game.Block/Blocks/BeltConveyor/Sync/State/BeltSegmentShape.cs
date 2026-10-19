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

        // 容量の式は構成(BeltSegmentLayout.Capacity)の1箇所だけに置き、ここは値として持つ。通信形にも値で載せ、クライアントは式を再実装しない
        // The capacity rule lives only in the layout (BeltSegmentLayout.Capacity); this holds the value, the wire form carries it too, and the client never re-implements the rule
        public readonly int Capacity;

        public BeltSegmentShape(BeltSegmentKind kind, bool isInternal, BeltCellShape[] cells, int capacity, int speed, BeltDirection forward, BeltLinkShape[] inputs, BeltLinkShape[] outputs)
        {
            Kind = kind;
            IsInternal = isInternal;
            Cells = cells;
            Capacity = capacity;
            Speed = speed;
            Forward = forward;
            Inputs = inputs;
            Outputs = outputs;
        }

        public static BeltSegmentShape FromLayout(BeltSegmentLayout layout)
        {
            var cells = new BeltCellShape[layout.Cells.Length];
            for (var i = 0; i < cells.Length; i++) cells[i] = new BeltCellShape(layout.Cells[i].Position, layout.Cells[i].Forward);
            return new BeltSegmentShape(layout.Kind, layout.IsInternal, cells, layout.Capacity, layout.Speed, layout.Forward, FromLinks(layout.Inputs), FromLinks(layout.Outputs));

            #region Internal

            BeltLinkShape[] FromLinks(BeltSegmentLayoutLink[] links)
            {
                // 機械の番兵は全量側の値へ明示的に写し、segment番号はそのまま写す
                // The machine sentinel maps explicitly to the full state's value; segment numbers copy over as they are
                var shapes = new BeltLinkShape[links.Length];
                for (var i = 0; i < links.Length; i++)
                {
                    var partner = links[i].IsMachine ? BeltLinkShape.Machine : links[i].PartnerSegmentIndex;
                    shapes[i] = new BeltLinkShape(links[i].Direction, links[i].EntryDirection, partner);
                }
                return shapes;
            }

            #endregion
        }
    }
}
