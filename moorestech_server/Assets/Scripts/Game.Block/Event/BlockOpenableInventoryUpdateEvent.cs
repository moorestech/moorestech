using System;
using Game.Block.Interface.Event;
using UniRx;

namespace Game.Block.Event
{
    public class BlockOpenableInventoryUpdateEvent : IBlockOpenableInventoryUpdateEvent
    {
        public IObservable<BlockOpenableInventoryUpdateEventProperties> OnInventoryUpdated => _subject;
        
        private readonly Subject<BlockOpenableInventoryUpdateEventProperties> _subject = new();
        
        public void OnInventoryUpdateInvoke(BlockOpenableInventoryUpdateEventProperties properties)
        {
            _subject.OnNext(properties);
        }
    }
}
