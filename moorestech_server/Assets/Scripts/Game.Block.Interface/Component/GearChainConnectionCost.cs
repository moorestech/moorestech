using Core.Master;

namespace Game.Block.Interface.Component
{
    public readonly struct GearChainConnectionCost
    {
        public GearChainConnectionCost(ItemId itemId, int count, int playerId, bool isOwner)
        {
            ItemId = itemId;
            Count = count;
            PlayerId = playerId;
            IsOwner = isOwner;
        }

        public ItemId ItemId { get; }
        public int Count { get; }
        public int PlayerId { get; }
        public bool IsOwner { get; }
    }
}
