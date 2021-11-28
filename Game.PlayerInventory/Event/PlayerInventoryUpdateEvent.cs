namespace PlayerInventory.Event
{
    public class PlayerInventoryUpdateEvent
    {
        public delegate void PutBlockEvent(PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties);
        public event PutBlockEvent OnPlayerInventoryUpdate;

        internal void OnPlayerInventoryUpdateInvoke(PlayerInventoryUpdateEventProperties playerInventoryUpdateEventProperties)
        {
            OnPlayerInventoryUpdate?.Invoke(playerInventoryUpdateEventProperties);
        }
    }
}