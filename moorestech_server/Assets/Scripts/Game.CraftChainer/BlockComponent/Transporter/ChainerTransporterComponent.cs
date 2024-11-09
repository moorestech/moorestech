using Game.Block.Interface.Component;

namespace Game.CraftChainer.BlockComponent
{
    public class ChainerTransporterComponent : IBlockComponent
    {
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}