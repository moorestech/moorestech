using Core.Block.Blocks;

namespace Game.Block.Interface.Factory
{
    public interface IBlockFactory
    {
        public IBlock Create(int blockId, int entityId);
        public IBlock Load(ulong blockHash, int entityId, string state);
    }
}