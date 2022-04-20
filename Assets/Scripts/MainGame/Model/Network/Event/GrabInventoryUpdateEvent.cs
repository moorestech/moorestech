using System;
using System.Collections.Generic;
using MainGame.Basic;

namespace MainGame.Model.Network.Event
{
    public class GrabInventoryUpdateEvent
    {
        public event Action<GrabInventoryUpdateEventProperties> OnGrabInventoryUpdateEvent;

        internal void GrabInventoryUpdateEventInvoke(GrabInventoryUpdateEventProperties obj)
        {
            OnGrabInventoryUpdateEvent?.Invoke(obj);
        }
    }

    public class GrabInventoryUpdateEventProperties
    {
        public readonly ItemStack ItemStack;

        public GrabInventoryUpdateEventProperties(ItemStack itemStack)
        {
            ItemStack = itemStack;
        }
    }
}