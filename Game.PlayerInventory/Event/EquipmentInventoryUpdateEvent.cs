using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.IEquipmentInventoryUpdateEvent;

namespace PlayerInventory.Event
{
    public class EquipmentInventoryUpdateEvent : IEquipmentInventoryUpdateEvent
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