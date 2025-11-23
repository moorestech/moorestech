namespace Game.Block.Interface
{
    public enum BlockRemoveReason
    {
        Broken,
        ManualRemove
    }

    public interface IBlockRemover
    {
        bool Remove(BlockInstanceId blockInstanceId, BlockRemoveReason reason);
    }
}
