using System;
using Core.Item;
using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public class CraftingEvent : ICraftingEvent
    {
        private event Action<(int id,int itemCount)> OnCraft; 
        public void Subscribe(Action<(int itemId, int itemCount)> onCraft)
        {
            OnCraft += onCraft;
        }

        internal void InvokeEvent(int id,int itemCount)
        {
            OnCraft?.Invoke((id,itemCount));
        }
        
    }
}