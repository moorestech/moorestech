using System.Collections.Generic;
using MainGame.Basic;
using static MainGame.Network.Event.IPlayerInventoryUpdateEvent;

namespace MainGame.Network.Event
{
    public interface IPlayerInventoryUpdateEvent
    {
        public delegate void PlayerInventoryUpdate(PlayerInventoryUpdateProperties properties);
        public delegate void PlayerInventorySlotUpdate(PlayerInventorySlotUpdateProperties properties);

        public void Subscribe(PlayerInventoryUpdate onPlayerInventoryUpdate, PlayerInventorySlotUpdate onPlayerInventorySlotUpdate);
    }
    public class PlayerInventoryUpdateEvent : IPlayerInventoryUpdateEvent
    {
        private event PlayerInventoryUpdate OnPlayerInventoryUpdateEvent;
        private event PlayerInventorySlotUpdate OnPlayerInventorySlotUpdateEvent;
        public void Subscribe(PlayerInventoryUpdate onPlayerInventoryUpdate, PlayerInventorySlotUpdate onPlayerInventorySlotUpdate)
        {
            OnPlayerInventoryUpdateEvent += onPlayerInventoryUpdate;
            OnPlayerInventorySlotUpdateEvent += onPlayerInventorySlotUpdate;
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