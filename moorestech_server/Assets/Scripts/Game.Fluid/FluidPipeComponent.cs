using Game.Block.Interface.Component;

namespace Game.Fluid
{
    public class FluidPipeComponent : IBlockComponent
    {
        
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}