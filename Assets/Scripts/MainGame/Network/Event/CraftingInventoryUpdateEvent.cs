using System;
using System.Collections.Generic;
using System.Threading;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class CraftingInventoryUpdateEvent
    {
        
        private SynchronizationContext _mainThread;
        
        public CraftingInventoryUpdateEvent()
        {
            //Unityではメインスレッドでしか実行できないのでメインスレッドを保存しておく
            _mainThread = SynchronizationContext.Current;
        }
        public event Action<CraftingInventoryUpdateProperties> OnCraftingInventoryUpdate;
        public event Action<CraftingInventorySlotUpdateProperties> OnCraftingInventorySlotUpdate;

        internal void InvokeCraftingInventorySlotUpdate(CraftingInventorySlotUpdateProperties properties)
        {
            _mainThread.Post(_ => OnCraftingInventorySlotUpdate?.Invoke(properties), null);
        }

        internal void InvokeCraftingInventoryUpdate(CraftingInventoryUpdateProperties properties)
        {
            _mainThread.Post(_ => OnCraftingInventoryUpdate?.Invoke(properties), null);
        }
    }
    
    
    public class CraftingInventoryUpdateProperties
    {
        public readonly int PlayerId;
        public readonly List<ItemStack> ItemStacks;
        public readonly ItemStack ResultItemStack;
        public readonly bool CanCraft;

        public CraftingInventoryUpdateProperties(int playerId, bool canCraft, List<ItemStack> itemStacks, ItemStack resultItemStack)
        {
            PlayerId = playerId;
            ItemStacks = itemStacks;
            ResultItemStack = resultItemStack;
            CanCraft = canCraft;
        }
    }

    public class CraftingInventorySlotUpdateProperties
    {
        public readonly int SlotId;
        public readonly ItemStack ItemStack;
        public readonly ItemStack ResultItemStack;
        public readonly bool CanCraft;

        public CraftingInventorySlotUpdateProperties(int slotId, ItemStack itemStack,ItemStack resultItemStack,bool canCraft)
        {
            SlotId = slotId;
            ItemStack = itemStack;
            ResultItemStack = resultItemStack;
            CanCraft = canCraft;
        }
    }
}