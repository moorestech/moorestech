using MainGame.Network.Interface;
using static MainGame.Network.Interface.IPlayerInventoryUpdateEvent;

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

        internal void OnOnPlayerInventoryUpdateEvent(
            OnPlayerInventoryUpdateProperties properties)
        {
            OnPlayerInventoryUpdateEvent?.Invoke(properties);
        }

        internal void OnOnPlayerInventorySlotUpdateEvent(
            OnPlayerInventorySlotUpdateProperties properties)
        {
            OnPlayerInventorySlotUpdateEvent?.Invoke(properties);
        }
    }
}