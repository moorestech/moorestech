using System;
using System.Collections.Generic;
using System.Threading;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class MainInventoryUpdateEvent
    {
        private SynchronizationContext _mainThread;
        
        public MainInventoryUpdateEvent()
        {
            _mainThread = SynchronizationContext.Current;
        }
        
        
        public event Action<MainInventoryUpdateProperties> OnMainInventoryUpdateEvent;
        public event Action<MainInventorySlotUpdateProperties> OnMainInventorySlotUpdateEvent;


        internal void InvokeMainInventoryUpdate(MainInventoryUpdateProperties properties)
        {
            _mainThread.Post(_ => OnMainInventoryUpdateEvent?.Invoke(properties), null);
        }

        internal void InvokeMainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            _mainThread.Post(_ => OnMainInventorySlotUpdateEvent?.Invoke(properties), null);
        }
    }
    
    

    public class MainInventoryUpdateProperties
    {
        public readonly int PlayerId;
        public readonly List<ItemStack> ItemStacks;

        public MainInventoryUpdateProperties(int playerId, List<ItemStack> itemStacks)
        {
            PlayerId = playerId;
            ItemStacks = itemStacks;
        }
    }

    public class MainInventorySlotUpdateProperties
    {
        public readonly int SlotId;
        public readonly ItemStack ItemStack;

        public MainInventorySlotUpdateProperties(int slotId, ItemStack itemStack)
        {
            SlotId = slotId;
            ItemStack = itemStack;
        }
    }
}