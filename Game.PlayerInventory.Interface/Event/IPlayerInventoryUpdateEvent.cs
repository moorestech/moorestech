namespace Game.PlayerInventory.Interface.Event
{
    public interface IPlayerInventoryUpdateEvent
    {
        public delegate void UpdateInventoryEvent(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);

        public void Subscribe(UpdateInventoryEvent updateInventoryEvent);
    }
}