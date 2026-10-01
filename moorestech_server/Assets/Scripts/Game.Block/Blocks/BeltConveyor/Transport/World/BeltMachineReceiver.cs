using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal sealed class BeltMachineReceiver : IBeltReceiver
    {
        private readonly BeltWorldTransport owner;
        private readonly BeltNetworkConnection edge;
        private readonly BeltMachineConnection connection;
        private readonly int stage;
        private int offer;
        internal BeltMachineReceiver(BeltWorldTransport owner, BeltNetworkConnection edge, BeltMachineConnection connection, int stage)
        { this.owner = owner; this.edge = edge; this.connection = connection; this.stage = stage; }
        public int GetOffer(BeltDirection direction)
        {
            // bufferの候補アイテムで問い合わせ、成功とは別に記録する。
            // Query using the buffer candidate and record acceptance separately from success.
            offer = 0;
            if (!owner.Network.TryGetBufferedItem(edge.SourceId, out var item)) return 0;
            var stack = owner.GetStack(item.Guid);
            if (connection.Target.InsertionCheck(new List<IItemStack> { stack })) offer = BeltConstants.ItemWidth;
            if (0 < offer) owner.RecordOutput(new BeltOutputResult(edge.SourceId, edge.TargetId, stage, edge.Direction, offer, false, item));
            return offer;
        }
        public bool TryReceive(BeltDirection direction, int length, in BeltItem item)
        {
            var stack = owner.GetStack(item.Guid);
            var context = new InsertItemContext(connection.Source, connection.Info.SelfConnector, connection.Info.TargetConnector);
            if (connection.Target.InsertItem(stack, context).Count != 0) return false;
            owner.RecordOutput(new BeltOutputResult(edge.SourceId, edge.TargetId, stage, edge.Direction, offer, true, item));
            return true;
        }
    }
}
