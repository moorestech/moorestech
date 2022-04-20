using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.ICraftInventoryUpdateEvent;

namespace PlayerInventory.Event
{
    public class CraftInventoryUpdateEvent : ICraftInventoryUpdateEvent
    {
        public event UpdateInventoryEvent OnPlayerInventoryUpdate;
        public void Subscribe(UpdateInventoryEvent updateInventoryEvent)
        {
            OnPlayerInventoryUpdate += updateInventoryEvent;
        }

        public void OnInventoryUpdateInvoke(
            PlayerInventoryUpdateEventProperties properties)
        {
            OnPlayerInventoryUpdate?.Invoke(properties);
        }
    }
}