using System;
using Game.Train.Unit;
using UniRx;

namespace Game.Train.Event
{
    public class TrainUpdateEvent : ITrainUpdateEvent
    {
        public IObservable<TrainInventoryUpdateEventProperties> OnInventoryUpdated => _inventorySubject;
        private readonly Subject<TrainInventoryUpdateEventProperties> _inventorySubject = new();
        
        public IObservable<TrainCarInstanceId> OnTrainCarRemoved => _removedSubject;
        private readonly Subject<TrainCarInstanceId> _removedSubject = new();
        
        public void InvokeInventoryUpdate(TrainInventoryUpdateEventProperties properties)
        {
            _inventorySubject.OnNext(properties);
        }
        
        public void InvokeTrainCarRemoved(TrainCarInstanceId trainCarInstanceId)
        {
            _removedSubject.OnNext(trainCarInstanceId);
        }
    }
}
