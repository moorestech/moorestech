namespace Game.Block.Interface.Component
{
    public interface IGearChainPole : IBlockComponent
    {
        BlockInstanceId BlockInstanceId { get; }
        float MaxConnectionDistance { get; }
        bool IsConnectionFull { get; }
        bool ContainsChainConnection(BlockInstanceId partnerId);
        bool TryAddChainConnection(BlockInstanceId partnerId, GearChainConnectionCost connectionCost);
        bool TryRemoveChainConnection(BlockInstanceId partnerId, out GearChainConnectionCost cost);
    }
}
