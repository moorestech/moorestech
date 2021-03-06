using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.IMainInventoryUpdateEvent;

namespace PlayerInventory.Event
{
    public class MainInventoryUpdateEvent : IMainInventoryUpdateEvent
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