using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Topology.Layout
{
    // segment同士、またはsegmentと機械の間の接続1本。入力側・出力側のどちらの一覧にも同じ型で載る
    // One link between two segments or between a segment and a machine; the same type appears in both input and output lists
    public readonly struct BeltSegmentLayoutLink
    {
        // 出力なら末尾マスから相手へ、入力なら先頭マスから送り元へ向かう水平方向
        // Horizontal direction from the last cell toward the partner for outputs, or from the head cell toward the source for inputs
        public readonly BeltDirection Direction;
        // 受け取る側のマスから見た12通りの進入方向
        // 12-way entry direction as seen from the receiving cell
        public readonly BeltEntryDirection EntryDirection;
        // 相手のsegment番号。相手が機械ならMachine(-1)
        // Partner segment index, or Machine (-1) when the partner is a machine
        public readonly int PartnerSegmentIndex;
        // 元になった解決済み接続。機械とのコネクター対・インベントリはここから読む
        // The resolved connection this link came from; machine connector pairs and inventories are read from here
        public readonly BeltTopologyConnection Connection;

        public const int Machine = -1;

        public bool IsMachine => PartnerSegmentIndex == Machine;

        public BeltSegmentLayoutLink(BeltDirection direction, BeltEntryDirection entryDirection, int partnerSegmentIndex, in BeltTopologyConnection connection)
        {
            Direction = direction;
            EntryDirection = entryDirection;
            PartnerSegmentIndex = partnerSegmentIndex;
            Connection = connection;
        }
    }
}
