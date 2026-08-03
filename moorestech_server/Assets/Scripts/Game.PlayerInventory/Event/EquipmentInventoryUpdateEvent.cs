using Game.PlayerInventory.Interface.Event;
using static Game.PlayerInventory.Interface.Event.IEquipmentInventoryUpdateEvent;

namespace Game.PlayerInventory.Event
{
    public class EquipmentInventoryUpdateEvent : IEquipmentInventoryUpdateEvent
    {
        public void Subscribe(UpdateInventoryEvent updateInventoryEvent)
        {
            OnEquipmentInventoryUpdate += updateInventoryEvent;
        }

        public void SubscribeSelectedEquipmentIndex(UpdateSelectedEquipmentIndexEvent updateSelectedEquipmentIndexEvent)
        {
            OnSelectedEquipmentIndexUpdate += updateSelectedEquipmentIndexEvent;
        }

        public event UpdateInventoryEvent OnEquipmentInventoryUpdate;

        public event UpdateSelectedEquipmentIndexEvent OnSelectedEquipmentIndexUpdate;

        public void OnInventoryUpdateInvoke(PlayerInventoryUpdateEventProperties properties)
        {
            OnEquipmentInventoryUpdate?.Invoke(properties);
        }

        public void OnSelectedEquipmentIndexUpdateInvoke(EquipmentSelectedIndexUpdateEventProperties properties)
        {
            OnSelectedEquipmentIndexUpdate?.Invoke(properties);
        }
    }
}
