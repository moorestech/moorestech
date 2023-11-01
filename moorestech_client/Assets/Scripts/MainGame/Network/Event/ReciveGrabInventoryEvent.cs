using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class ReceiveGrabInventoryEvent
    {
        public event Action<GrabInventoryUpdateEventProperties> OnGrabInventoryUpdateEvent;

        internal async UniTask OnGrabInventoryUpdateEventInvoke(GrabInventoryUpdateEventProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnGrabInventoryUpdateEvent?.Invoke(properties);
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