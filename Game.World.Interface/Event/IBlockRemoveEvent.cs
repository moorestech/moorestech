namespace Game.World.Interface.Event
{
    public interface IBlockRemoveEvent
    {
        
        public delegate void BlockPlaceEvent(BlockRemoveEventProperties blockPlaceEventProperties);
        
        public void Subscribe(BlockPlaceEvent blockRemoveEvent);
    }
}