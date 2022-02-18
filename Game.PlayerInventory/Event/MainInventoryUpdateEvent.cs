using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public class MainInventoryUpdateEvent : IMainInventoryUpdateEvent,IPlayerInventoryUpdateEvent
    {
        public event IMainInventoryUpdateEvent.UpdateInventoryEvent OnPlayerInventoryUpdate;


        public void Subscribe(IMainInventoryUpdateEvent.UpdateInventoryEvent updateInventoryEvent)
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