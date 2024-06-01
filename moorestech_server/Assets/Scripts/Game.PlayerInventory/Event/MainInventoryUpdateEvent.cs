using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.IMainInventoryUpdateEvent;

namespace Game.PlayerInventory.Event
{
    public class MainInventoryUpdateEvent : IMainInventoryUpdateEvent
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