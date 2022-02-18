namespace Game.PlayerInventory.Interface.Event
{
    public interface ICraftInventoryUpdateEvent
    {
        public delegate void UpdateInventoryEvent(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);

        public void Subscribe(UpdateInventoryEvent updateInventoryEvent);
    }
}