using Game.Block.Interface.Component;

namespace Game.Train.Station
{
    public class StationComponent : IBlockComponent
    {
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}