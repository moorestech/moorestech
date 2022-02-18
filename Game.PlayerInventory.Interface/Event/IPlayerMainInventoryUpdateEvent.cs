namespace Game.PlayerInventory.Interface.Event
{
    public interface IPlayerMainInventoryUpdateEvent
    {
        public delegate void UpdateInventoryEvent(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);

        public void Subscribe(UpdateInventoryEvent updateInventoryEvent);
    }
}