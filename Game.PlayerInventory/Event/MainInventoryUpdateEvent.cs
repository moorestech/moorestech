using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.IMainInventoryUpdateEvent;

namespace PlayerInventory.Event
{
    public class MainInventoryUpdateEvent : IMainInventoryUpdateEvent,IPlayerInventoryUpdateEvent
    {
        public event UpdateInventoryEvent OnPlayerInventoryUpdate;


        public void Subscribe(UpdateInventoryEvent updateInventoryEvent)
        {
            OnPlayerInventoryUpdate += updateInventoryEvent;
        }

        public void OnMainInventoryUpdateInvoke(
            PlayerInventoryUpdateEventProperties properties)
        {
            OnPlayerInventoryUpdate?.Invoke(properties);
        }
    }
}