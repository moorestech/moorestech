using Game.Block.Interface;

namespace Game.Block.Interface
{
    public interface IBlockFactory
    {
        public IBlock Create(int blockId, int entityId);
        public IBlock Load(long blockHash, int entityId, string state);
    }
}