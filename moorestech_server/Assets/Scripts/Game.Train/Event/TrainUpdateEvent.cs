using System;
using UniRx;

namespace Game.Train.Event
{
    public class TrainUpdateEvent : ITrainUpdateEvent
    {
        public IObservable<TrainInventoryUpdateEventProperties> OnInventoryUpdated => _inventorySubject;
        private readonly Subject<TrainInventoryUpdateEventProperties> _inventorySubject = new();
        
        public IObservable<Guid> OnTrainRemoved => _removedSubject;
        private readonly Subject<Guid> _removedSubject = new();
        
        public void InvokeInventoryUpdate(TrainInventoryUpdateEventProperties properties)
        {
            _inventorySubject.OnNext(properties);
        }
        
        public void InvokeTrainRemoved(Guid trainId)
        {
            _removedSubject.OnNext(trainId);
        }
    }
}
