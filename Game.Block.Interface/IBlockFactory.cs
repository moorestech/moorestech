namespace Game.Block.Interface
{
    public interface IBlockFactory
    {
        public IBlock Create(int blockId, int entityId);
        public IBlock Load(ulong blockHash, int entityId, string state);
    }
}