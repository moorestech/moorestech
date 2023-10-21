using System;

namespace Game.PlayerInventory.Interface.Event
{
    public interface ICraftingEvent
    {
        //IItemStackint
        public void Subscribe(Action<(int itemId, int itemCount)> onCraft);
    }
}