using Game.Block.Interface.Component;

namespace Game.Block.Blocks.ItemShooter
{
    public class ItemShooterComponent : IBlockComponent
    {
        public bool IsDestroy { get; }
        public void Destroy()
        {
            throw new System.NotImplementedException();
        }
    }
}