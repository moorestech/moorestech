using System.Collections.Generic;
using MainGame.Basic;
using static MainGame.Network.Event.IMainInventoryUpdateEvent;

namespace MainGame.Network.Event
{
    public interface IMainInventoryUpdateEvent
    {
        public delegate void MainInventoryUpdate(MainInventoryUpdateProperties properties);
        public delegate void MainInventorySlotUpdate(MainInventorySlotUpdateProperties properties);

        public void Subscribe(MainInventoryUpdate onMainInventoryUpdate, MainInventorySlotUpdate onMainInventorySlotUpdate);
    }
    public class MainInventoryUpdateEvent : IMainInventoryUpdateEvent
    {
        private event MainInventoryUpdate OnMainInventoryUpdateEvent;
        private event MainInventorySlotUpdate OnMainInventorySlotUpdateEvent;
        public void Subscribe(MainInventoryUpdate onMainInventoryUpdate, MainInventorySlotUpdate onMainInventorySlotUpdate)
        {
            OnMainInventoryUpdateEvent += onMainInventoryUpdate;
            OnMainInventorySlotUpdateEvent += onMainInventorySlotUpdate;
        }

        public void InvokeMainInventoryUpdate(
            MainInventoryUpdateProperties properties)
        {
            OnMainInventoryUpdateEvent?.Invoke(properties);
        }

        public void InvokeMainInventorySlotUpdate(
            MainInventorySlotUpdateProperties properties)
        {
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