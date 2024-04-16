using Game.Block.Interface.ComponentAttribute;

namespace Game.Block.Interface.Component
{
    [DisallowMultiple]
    public interface IBlockSaveState : IBlockComponent
    {
        string GetSaveState();
    }
}