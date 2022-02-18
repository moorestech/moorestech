using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public interface IPlayerInventoryUpdateEvent
    {
        public void OnMainInventoryUpdateInvoke(PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);
    }
}