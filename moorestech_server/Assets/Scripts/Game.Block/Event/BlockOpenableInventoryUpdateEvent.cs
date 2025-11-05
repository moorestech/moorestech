using System;
using Game.Block.Interface.Event;
using UniRx;

namespace Game.Block.Event
{
    public class BlockOpenableInventoryUpdateEvent : IBlockOpenableInventoryUpdateEvent
    {
        private readonly Subject<BlockOpenableInventoryUpdateEventProperties> _subject = new();
        public IObservable<BlockOpenableInventoryUpdateEventProperties> OnInventoryUpdated => _subject;
        
        public IDisposable Subscribe(Action<BlockOpenableInventoryUpdateEventProperties> blockInventoryEvent)
        {
            return _subject.Subscribe(blockInventoryEvent);
        }
        
        public void OnInventoryUpdateInvoke(BlockOpenableInventoryUpdateEventProperties properties)
        {
            _subject.OnNext(properties);
        }
    }
}
