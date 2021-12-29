using Core.Electric;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace World.EventListener
{
    public class ConnectElectricSegment
    {
        public ConnectElectricSegment(IBlockPlaceEvent blockPlaceEvent,IWorldBlockComponentDatastore<IBlockElectric> electricDatastore)
        {
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
        }
    }
}