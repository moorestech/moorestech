using System;
using Core.Item;
using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public class CraftingEvent : ICraftingEvent
    {
        private event Action<IItemStack> OnCraft; 
        public void Subscribe(Action<IItemStack> onCraft)
        {
            OnCraft += onCraft;
        }

        internal void InvokeEvent(IItemStack craftItem)
        {
            OnCraft?.Invoke(craftItem);
        }
        
    }
}