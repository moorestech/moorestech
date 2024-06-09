namespace Game.Block.Interface
{
    public interface IBlockFactory
    {
        public IBlock Create(int blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo);
        public IBlock Load(long blockHash, BlockInstanceId blockInstanceId, string state, BlockPositionInfo blockPositionInfo);
    }
}