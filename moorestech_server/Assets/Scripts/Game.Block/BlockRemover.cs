using Game.Block.Interface;
using Game.Context;

namespace Game.Block
{
    public class BlockRemover : IBlockRemover
    {
        public bool Remove(BlockInstanceId blockInstanceId, BlockRemoveReason reason)
        {
            var datastore = ServerContext.WorldBlockDatastore;
            if (datastore.GetBlock(blockInstanceId) is null) return false;
            var position = datastore.GetBlockPosition(blockInstanceId);
            return datastore.RemoveBlock(position, reason);
        }
    }
}
