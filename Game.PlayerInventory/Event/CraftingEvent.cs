using System;
using Game.PlayerInventory.Interface.Event;

namespace PlayerInventory.Event
{
    public class CraftingEvent : ICraftingEvent
    {
        public void Subscribe(Action<(int itemId, int itemCount)> onCraft)
        {
            OnCraft += onCraft;
        }

        private event Action<(int id, int itemCount)> OnCraft;

        internal void InvokeEvent(int id, int itemCount)
        {
            OnCraft?.Invoke((id, itemCount));
        }
    }
}