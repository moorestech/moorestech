using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.IGrabInventoryUpdateEvent;

namespace PlayerInventory.Event
{
    public class GrabInventoryUpdateEvent : IGrabInventoryUpdateEvent
    {
        public event UpdateInventoryEvent OnPlayerInventoryUpdate;

        public void OnInventoryUpdateInvoke(
            PlayerInventoryUpdateEventProperties properties)
        {
            OnPlayerInventoryUpdate?.Invoke(properties);
        }

        public void Subscribe(UpdateInventoryEvent updateInventoryEvent)
        {
            OnPlayerInventoryUpdate += updateInventoryEvent;
        }
    }
}