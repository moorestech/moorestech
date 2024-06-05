namespace Game.Block.Interface
{
    public interface IBlockFactory
    {
        public IBlock Create(int blockId, EntityID entityId, BlockPositionInfo blockPositionInfo);
        public IBlock Load(long blockHash, EntityID entityId, string state, BlockPositionInfo blockPositionInfo);
    }
}