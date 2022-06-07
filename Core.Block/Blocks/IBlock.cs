namespace Core.Block.Blocks
{
    public interface IBlock
    {
        public int EntityId { get;}
        public int BlockId { get;}
        public ulong BlockHash { get;}
        public string GetSaveState();
    }
}