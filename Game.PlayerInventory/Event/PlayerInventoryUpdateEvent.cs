using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public class PlayerInventoryUpdateEvent : IPlayerInventoryUpdateEvent
    {
        public event IPlayerInventoryUpdateEvent.UpdateInventoryEvent OnPlayerInventoryUpdate;


        public void Subscribe(IPlayerInventoryUpdateEvent.UpdateInventoryEvent updateInventoryEvent)
        {
            OnPlayerInventoryUpdate += updateInventoryEvent;
        }

        public void OnPlayerInventoryUpdateInvoke(PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties)
        {
            OnPlayerInventoryUpdate?.Invoke(playerInventoryUpdateEventProperties);
        }
    }
}