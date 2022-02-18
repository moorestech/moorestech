namespace Game.World.Interface.Event
{
    public interface IBlockInventoryUpdateEvent
    {
        public delegate void InventoryUpdateEvent(BlockInventoryUpdateEventProperties blockPlaceEventProperties);

        public void Subscribe(InventoryUpdateEvent blockPlaceEvent);
    }
}