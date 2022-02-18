using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public class PlayerMainInventoryUpdateEvent : IPlayerMainInventoryUpdateEvent,IPlayerInventoryUpdateEvent
    {
        public event IPlayerMainInventoryUpdateEvent.UpdateInventoryEvent OnPlayerInventoryUpdate;


        public void Subscribe(IPlayerMainInventoryUpdateEvent.UpdateInventoryEvent updateInventoryEvent)
        {
            OnPlayerInventoryUpdate += updateInventoryEvent;
        }

        public void OnPlayerMainInventoryUpdateInvoke(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties)
        {
            OnPlayerInventoryUpdate?.Invoke(playerInventoryUpdateEventProperties);
        }
    }
}