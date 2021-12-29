using Game.World.Interface.Event;

namespace World.EventListener
{
    public class ConnectElectricSegment
    {
        public ConnectElectricSegment(IBlockPlaceEvent blockPlaceEvent)
        {
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
        }
    }
}