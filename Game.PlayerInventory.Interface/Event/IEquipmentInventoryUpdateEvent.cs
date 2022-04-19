namespace Game.PlayerInventory.Interface.Event
{
    public interface IEquipmentInventoryUpdateEvent
    {
        public delegate void UpdateInventoryEvent(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);

        public void Subscribe(UpdateInventoryEvent updateInventoryEvent);
    }
}