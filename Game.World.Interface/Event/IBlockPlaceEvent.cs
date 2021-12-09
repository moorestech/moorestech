namespace Game.World.Interface.Event
{
    public interface IBlockPlaceEvent
    {
        
        public delegate void BlockPlaceEvent(BlockPlaceEventProperties blockPlaceEventProperties);
        
        public void Subscribe(BlockPlaceEvent blockPlaceEvent);
        public void OnBlockPutEventInvoke(BlockPlaceEventProperties blockPlaceEventProperties);
    }
}