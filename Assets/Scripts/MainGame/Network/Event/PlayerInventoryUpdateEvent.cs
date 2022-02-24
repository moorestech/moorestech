using System.Collections.Generic;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class PlayerInventoryUpdateEvent 
    {
        public delegate void OnPlayerInventoryUpdate(PlayerInventoryUpdateProperties properties);
        public delegate void OnPlayerInventorySlotUpdate(PlayerInventorySlotUpdateProperties properties);
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
            PlayerInventoryUpdateProperties properties)
        {
            OnPlayerInventoryUpdateEvent?.Invoke(properties);
        }

        public void OnOnPlayerInventorySlotUpdateEvent(
            PlayerInventorySlotUpdateProperties properties)
        {
            OnPlayerInventorySlotUpdateEvent?.Invoke(properties);
        }
    }
    
    

    public class PlayerInventoryUpdateProperties
    {
        public readonly int PlayerId;
        public readonly List<ItemStack> ItemStacks;

        public PlayerInventoryUpdateProperties(int playerId, List<ItemStack> itemStacks)
        {
            PlayerId = playerId;
            ItemStacks = itemStacks;
        }
    }

    public class PlayerInventorySlotUpdateProperties
    {
        public readonly int SlotId;
        public readonly ItemStack ItemStack;

        public PlayerInventorySlotUpdateProperties(int slotId, ItemStack itemStack)
        {
            SlotId = slotId;
            ItemStack = itemStack;
        }
    }
}