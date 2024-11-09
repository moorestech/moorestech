
namespace Game.Block.Interface.Component
{
    public interface IBlockSaveState : IBlockComponent
    {
        public string SaveKey { get; }
        string GetSaveState();
    }
}