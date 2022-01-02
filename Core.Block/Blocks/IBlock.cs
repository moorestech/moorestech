namespace Core.Block.Blocks
{
    public interface IBlock
    {
        public int GetIntId();
        public int GetBlockId();
        public string GetSaveState();
    }
}