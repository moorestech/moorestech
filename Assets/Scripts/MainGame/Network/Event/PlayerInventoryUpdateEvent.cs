using System.Collections.Generic;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class PlayerInventoryUpdateEvent 
    {
        public delegate void OnPlayerInventoryUpdate(OnPlayerInventoryUpdateProperties properties);
        public delegate void OnPlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties);
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
    
    

    public class OnPlayerInventoryUpdateProperties
    {
        public readonly int PlayerId;
        public readonly List<ItemStack> ItemStacks;

        public OnPlayerInventoryUpdateProperties(int playerId, List<ItemStack> itemStacks)
        {
            PlayerId = playerId;
            ItemStacks = itemStacks;
        }
    }

    public class OnPlayerInventorySlotUpdateProperties
    {
        public readonly int SlotId;
        public readonly ItemStack ItemStack;

        public OnPlayerInventorySlotUpdateProperties(int slotId, ItemStack itemStack)
        {
            SlotId = slotId;
            ItemStack = itemStack;
        }
    }
}