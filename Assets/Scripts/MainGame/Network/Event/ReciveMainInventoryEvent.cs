using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class ReceiveMainInventoryEvent
    {
        public event Action<MainInventoryUpdateProperties> OnMainInventoryUpdateEvent;
        public event Action<MainInventorySlotUpdateProperties> OnMainInventorySlotUpdateEvent;


        internal void InvokeMainInventoryUpdate(MainInventoryUpdateProperties properties)
        {
            InvokeMainInventoryUpdateAsync(properties).Forget();
        }
        private async UniTask InvokeMainInventoryUpdateAsync(MainInventoryUpdateProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnMainInventoryUpdateEvent?.Invoke(properties);
        }
        
        
        

        internal void InvokeMainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            InvokeMainInventorySlotUpdateAsync(properties).Forget();
        }
        private async UniTask InvokeMainInventorySlotUpdateAsync(MainInventorySlotUpdateProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnMainInventorySlotUpdateEvent?.Invoke(properties);
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