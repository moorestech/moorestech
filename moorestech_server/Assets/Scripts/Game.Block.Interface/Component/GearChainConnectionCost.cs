using Core.Master;

namespace Game.Block.Interface.Component
{
    public readonly struct GearChainConnectionCost
    {
        public GearChainConnectionCost(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public ItemId ItemId { get; }
        public int Count { get; }
    }
}
