namespace Game.PlayerInventory.Interface.Event
{
    public interface IMainInventoryUpdateEvent
    {
        public delegate void UpdateInventoryEvent(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);

        public void Subscribe(UpdateInventoryEvent updateInventoryEvent);
    }
}