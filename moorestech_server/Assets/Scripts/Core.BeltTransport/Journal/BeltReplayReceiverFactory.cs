using System;
namespace Core.BeltTransport
{
    internal sealed class BeltReplayReceiverFactory : IBeltExternalReceiverFactory
    {
        private BeltOutputResult[] outputs = new BeltOutputResult[0];
        internal void SetResults(BeltOutputResult[] results) => outputs = results;
        public IBeltReceiver Create(BeltNetworkConnection connection, int stage) => new Receiver(this, connection.SourceId, connection.TargetId, stage, connection.Direction);
        private sealed class Receiver : IBeltReceiver
        {
            private readonly BeltReplayReceiverFactory owner;
            private readonly int sourceId, targetId, stage;
            private readonly BeltDirection direction;
            internal Receiver(BeltReplayReceiverFactory owner, int sourceId, int targetId, int stage, BeltDirection direction)
            { this.owner = owner; this.sourceId = sourceId; this.targetId = targetId; this.stage = stage; this.direction = direction; }
            public void AttachInput(IBeltSource source, BeltDirection inputDirection) { }
            public int GetOffer(BeltDirection inputDirection)
            {
                foreach (var result in owner.outputs)
                    if (result.SourceCellId == sourceId && result.TargetId == targetId && result.Stage == stage && result.Direction == direction) return result.Offer;
                return 0;
            }
            public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
            {
                foreach (var result in owner.outputs)
                {
                    if (result.SourceCellId != sourceId || result.TargetId != targetId || result.Stage != stage || result.Direction != direction || !result.Succeeded) continue;
                    if (result.Item.Guid != item.Guid || result.Item.ItemId != item.ItemId) throw new InvalidOperationException("Belt output identity differs from the committed journal.");
                    return true;
                }
                return false;
            }
        }
    }
}
