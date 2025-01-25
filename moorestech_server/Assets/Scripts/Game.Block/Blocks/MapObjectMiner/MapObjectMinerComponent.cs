using Game.Block.Interface.Component;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class MapObjectMinerComponent : IBlockComponent
    {
        
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}