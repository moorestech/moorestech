using System.Collections.Generic;
using MainGame.Basic;
using static MainGame.Network.Event.ICraftingInventoryUpdateEvent;

namespace MainGame.Network.Event
{
    public interface ICraftingInventoryUpdateEvent
    {
        public delegate void CraftingInventoryUpdate(CraftingInventoryUpdateProperties properties);
        public delegate void CraftingInventorySlotUpdate(CraftingInventorySlotUpdateProperties properties);

        public void Subscribe(CraftingInventoryUpdate craftingInventoryUpdate, CraftingInventorySlotUpdate craftingInventorySlotUpdate);
    }
    public class CraftingInventoryUpdateEvent : ICraftingInventoryUpdateEvent
    {
        private event CraftingInventoryUpdate OnCraftingInventoryUpdate;
        private event CraftingInventorySlotUpdate OnCraftingInventorySlotUpdate;
        
        public void Subscribe(CraftingInventoryUpdate craftingInventoryUpdate,
            CraftingInventorySlotUpdate craftingInventorySlotUpdate)
        {
            
        }

        protected virtual void InvokeCraftingInventorySlotUpdate(CraftingInventorySlotUpdateProperties properties)
        {
            OnCraftingInventorySlotUpdate?.Invoke(properties);
        }

        protected virtual void InvokeCraftingInventoryUpdate(CraftingInventoryUpdateProperties properties)
        {
            OnCraftingInventoryUpdate?.Invoke(properties);
        }
    }
    
    
    public class CraftingInventoryUpdateProperties
    {
        public readonly int PlayerId;
        public readonly List<ItemStack> ItemStacks;

        public CraftingInventoryUpdateProperties(int playerId, List<ItemStack> itemStacks)
        {
            PlayerId = playerId;
            ItemStacks = itemStacks;
        }
    }

    public class CraftingInventorySlotUpdateProperties
    {
        public readonly int SlotId;
        public readonly ItemStack ItemStack;

        public CraftingInventorySlotUpdateProperties(int slotId, ItemStack itemStack)
        {
            SlotId = slotId;
            ItemStack = itemStack;
        }
    }
}