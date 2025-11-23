namespace Game.Block.Interface
{
    public enum BlockRemoveReason
    {
        Broken,
        ManualRemove
    }

    public interface IBlockRemover
    {
        void Remove(BlockInstanceId blockInstanceId, BlockRemoveReason reason);
    }
}

