namespace Game.PlayerInventory.Interface.Event
{
    public interface IEquipmentInventoryUpdateEvent
    {
        public delegate void UpdateInventoryEvent(
            PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);

        public delegate void UpdateSelectedEquipmentIndexEvent(
            EquipmentSelectedIndexUpdateEventProperties equipmentSelectedIndexUpdateEventProperties);

        public void Subscribe(UpdateInventoryEvent updateInventoryEvent);

        // 装備スロットの中身と選択中スロットは別々に変化するため購読口を分ける
        // Slot contents and the selected slot change independently, so they get separate subscriptions
        public void SubscribeSelectedEquipmentIndex(UpdateSelectedEquipmentIndexEvent updateSelectedEquipmentIndexEvent);
    }
}
