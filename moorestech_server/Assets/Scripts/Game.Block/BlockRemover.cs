using Game.Block.Interface;
using Game.World.Interface.DataStore;

namespace Game.Block
{
    public class BlockRemover : IBlockRemover
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockRemover(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public bool Remove(BlockInstanceId blockInstanceId, BlockRemoveReason reason)
        {
            if (_worldBlockDatastore.GetBlock(blockInstanceId) is null) return false;
            var position = _worldBlockDatastore.GetBlockPosition(blockInstanceId);
            return _worldBlockDatastore.RemoveBlock(position, reason);
        }
    }
}
