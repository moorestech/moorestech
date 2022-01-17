using System.Collections.Generic;
using Maingame.Types;

namespace MainGame.Network.Interface
{
    public interface IPlayerInventoryUpdateEvent
    {
        public delegate void OnPlayerInventoryUpdate(OnPlayerInventoryUpdateProperties properties);
        public delegate void OnPlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties);

        public void Subscribe(
            OnPlayerInventoryUpdate onPlayerInventoryUpdate,
            OnPlayerInventorySlotUpdate onPlayerInventorySlotUpdate);
        public void Unsubscribe(
            OnPlayerInventoryUpdate onPlayerInventoryUpdate,
            OnPlayerInventorySlotUpdate onPlayerInventorySlotUpdate);

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