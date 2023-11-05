using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class ReceiveMainInventoryEvent
    {
        public event Action<MainInventoryUpdateProperties> OnMainInventoryUpdateEvent;
        public event Action<MainInventorySlotUpdateProperties> OnMainInventorySlotUpdateEvent;


        internal async UniTask InvokeMainInventoryUpdate(MainInventoryUpdateProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnMainInventoryUpdateEvent?.Invoke(properties);
        }


        internal async UniTask InvokeMainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnMainInventorySlotUpdateEvent?.Invoke(properties);
        }
    }


    public class MainInventoryUpdateProperties
    {
        public readonly List<ItemStack> ItemStacks;
        public readonly int PlayerId;

        public MainInventoryUpdateProperties(int playerId, List<ItemStack> itemStacks)
        {
            PlayerId = playerId;
            ItemStacks = itemStacks;
        }
    }

    public class MainInventorySlotUpdateProperties
    {
        public readonly ItemStack ItemStack;
        public readonly int SlotId;

        public MainInventorySlotUpdateProperties(int slotId, ItemStack itemStack)
        {
            SlotId = slotId;
            ItemStack = itemStack;
        }
    }
}