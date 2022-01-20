using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using static MainGame.Network.Interface.Receive.IPlayerInventoryUpdateEvent;

namespace MainGame.Network.Event
{
    public class PlayerInventoryUpdateEvent : IPlayerInventoryUpdateEvent
    {
        private event OnPlayerInventoryUpdate OnPlayerInventoryUpdateEvent;
        private event OnPlayerInventorySlotUpdate OnPlayerInventorySlotUpdateEvent;
        public void Subscribe(
            OnPlayerInventoryUpdate onPlayerInventoryUpdate,
            OnPlayerInventorySlotUpdate onPlayerInventorySlotUpdate)
        {
            OnPlayerInventoryUpdateEvent += onPlayerInventoryUpdate;
            OnPlayerInventorySlotUpdateEvent += onPlayerInventorySlotUpdate;
        }

        public void Unsubscribe(
            OnPlayerInventoryUpdate onPlayerInventoryUpdate,
            OnPlayerInventorySlotUpdate onPlayerInventorySlotUpdate)
        {
            OnPlayerInventoryUpdateEvent -= onPlayerInventoryUpdate;
            OnPlayerInventorySlotUpdateEvent -= onPlayerInventorySlotUpdate;
        }

        public void OnOnPlayerInventoryUpdateEvent(
            OnPlayerInventoryUpdateProperties properties)
        {
            OnPlayerInventoryUpdateEvent?.Invoke(properties);
        }

        public void OnOnPlayerInventorySlotUpdateEvent(
            OnPlayerInventorySlotUpdateProperties properties)
        {
            OnPlayerInventorySlotUpdateEvent?.Invoke(properties);
        }
    }
}