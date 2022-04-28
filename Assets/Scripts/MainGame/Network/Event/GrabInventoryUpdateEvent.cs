using System;
using System.Threading;
using MainGame.Basic;

namespace MainGame.Network.Event
{
    public class GrabInventoryUpdateEvent
    {
        private SynchronizationContext _mainThread;
        
        public GrabInventoryUpdateEvent()
        {
            //Unityではメインスレッドでしか実行できないのでメインスレッドを保存しておく
            _mainThread = SynchronizationContext.Current;
        }
        public event Action<GrabInventoryUpdateEventProperties> OnGrabInventoryUpdateEvent;

        internal void GrabInventoryUpdateEventInvoke(GrabInventoryUpdateEventProperties obj)
        {
            _mainThread.Post(_ => OnGrabInventoryUpdateEvent?.Invoke(obj), null);
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