namespace Game.PlayerInventory.Interface.Event
{
    public interface IGrabInventoryUpdateEvent
    {
        public delegate void UpdateInventoryEvent(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);
        
        public void Subscribe(UpdateInventoryEvent updateInventoryEvent);
    }
}