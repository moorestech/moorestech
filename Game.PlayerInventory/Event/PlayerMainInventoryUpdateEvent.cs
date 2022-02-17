using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public class PlayerMainInventoryUpdateEvent : IPlayerMainInventoryUpdateEvent
    {
        public event IPlayerMainInventoryUpdateEvent.UpdateInventoryEvent OnPlayerInventoryUpdate;


        public void Subscribe(IPlayerMainInventoryUpdateEvent.UpdateInventoryEvent updateInventoryEvent)
        {
            OnPlayerInventoryUpdate += updateInventoryEvent;
        }

        public void OnPlayerInventoryUpdateInvoke(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties)
        {
            OnPlayerInventoryUpdate?.Invoke(playerInventoryUpdateEventProperties);
        }
    }
}