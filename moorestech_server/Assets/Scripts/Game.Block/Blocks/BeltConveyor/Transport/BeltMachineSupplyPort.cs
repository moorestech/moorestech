using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 機械がベルコンのblockへ押し込むときの受け口。合流なら内部segment、通常・分岐ならそのsegmentが受ける
    // The receiving end for a machine pushing into a belt block: the internal segment for a merge, or the segment itself for a normal or branch
    public readonly struct BeltMachineSupplyPort
    {
        // 押し込まれる側のベルコンのblock
        // The belt block being pushed into
        public readonly BlockInstanceId BeltBlockInstanceId;
        // 機械側の接続。送り元blockとコネクター対の照合に使う
        // The machine-side connection, used to match the sender block and connector pair
        public readonly BeltTopologyConnection Connection;
        public readonly IBeltReceiver Receiver;
        // 受け側から見た搬入元の方向と、受け側マスへの進入方向
        // Source direction as seen from the receiver, and the entry direction into the receiving cell
        public readonly BeltDirection Direction;
        public readonly BeltEntryDirection EntryDirection;

        public BeltMachineSupplyPort(BlockInstanceId beltBlockInstanceId, in BeltTopologyConnection connection, IBeltReceiver receiver,
            BeltDirection direction, BeltEntryDirection entryDirection)
        {
            BeltBlockInstanceId = beltBlockInstanceId;
            Connection = connection;
            Receiver = receiver;
            Direction = direction;
            EntryDirection = entryDirection;
        }

        // 送り元blockと両コネクターが一致する押し込みだけを受ける
        // Accept only a push whose sender block and both connectors match
        public bool Matches(BlockInstanceId beltBlockInstanceId, in InsertItemContext context)
        {
            return BeltBlockInstanceId == beltBlockInstanceId && Connection.PartnerBlock.BlockInstanceId == context.SourceBlockInstanceId &&
                   ReferenceEquals(Connection.SourceConnector, context.SourceConnector) && ReferenceEquals(Connection.TargetConnector, context.TargetConnector);
        }
    }
}
