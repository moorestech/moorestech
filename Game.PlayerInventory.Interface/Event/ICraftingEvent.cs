using System;
using Core.Item;

namespace Game.PlayerInventory.Interface.Event
{
    public interface ICraftingEvent
    {
        TODO
        public void Subscribe(Action<IItemStack> onCraft);
    }
}