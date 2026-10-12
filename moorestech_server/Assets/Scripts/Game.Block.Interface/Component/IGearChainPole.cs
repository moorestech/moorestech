namespace Game.Block.Interface.Component
{
    public interface IGearChainPole : IBlockComponent
    {
        BlockInstanceId BlockInstanceId { get; }
        float MaxConnectionDistance { get; }
        bool IsConnectionFull { get; }
        bool ContainsChainConnection(BlockInstanceId partnerId);
        bool TryAddChainConnection(BlockInstanceId partnerId, ConnectionLineRecord connectionRecord);
        // 切断前の返却検査に使う記録を読む
        // Read the record for the refund check before disconnecting
        bool TryGetChainConnectionRecord(BlockInstanceId partnerId, out ConnectionLineRecord record);
        bool TryRemoveChainConnection(BlockInstanceId partnerId, out ConnectionLineRecord record);
    }
}
