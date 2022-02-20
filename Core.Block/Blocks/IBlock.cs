namespace Core.Block.Blocks
{
    public interface IBlock
    {
        public int GetEntityId();
        public int GetBlockId();
        public string GetSaveState();
    }
}