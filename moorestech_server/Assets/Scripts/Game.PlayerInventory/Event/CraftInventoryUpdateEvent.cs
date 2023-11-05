using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.ICraftInventoryUpdateEvent;

namespace Game.PlayerInventory.Event
{
    public class CraftInventoryUpdateEvent : ICraftInventoryUpdateEvent
    {
        public void Subscribe(UpdateInventoryEvent updateInventoryEvent)
        {
            OnPlayerInventoryUpdate += updateInventoryEvent;
        }

        public event UpdateInventoryEvent OnPlayerInventoryUpdate;

        public void OnInventoryUpdateInvoke(
            PlayerInventoryUpdateEventProperties properties)
        {
            OnPlayerInventoryUpdate?.Invoke(properties);
        }
    }
}