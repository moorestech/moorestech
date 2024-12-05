using Game.Block.Interface.Component;

namespace Game.Train.Blocks
{
    public class RailComponent : IBlockComponent
    {
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}