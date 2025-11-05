using System;
using Core.Item.Interface;
using UniRx;

namespace Game.Context.Event
{
    /// <summary>
    /// 列車インベントリ更新イベントのデータ
    /// Data payload for train inventory updates.
    /// </summary>
    public class TrainInventoryUpdateEventProperties
    {
        public Guid TrainId { get; }
        public int Slot { get; }
        public IItemStack ItemStack { get; }
        
        public TrainInventoryUpdateEventProperties(Guid trainId, int slot, IItemStack itemStack)
        {
            TrainId = trainId;
            Slot = slot;
            ItemStack = itemStack;
        }
    }
    
    /// <summary>
    /// 列車関連イベント（インベントリ更新・削除）を扱うインターフェース
    /// Interface that exposes train inventory update and removal events.
    /// </summary>
    public interface ITrainUpdateEvent
    {
        IObservable<TrainInventoryUpdateEventProperties> OnInventoryUpdated { get; }
        IObservable<Guid> OnTrainRemoved { get; }
        
        IDisposable SubscribeInventory(Action<TrainInventoryUpdateEventProperties> handler);
        IDisposable SubscribeRemoval(Action<Guid> handler);
        
        void PublishInventoryUpdate(TrainInventoryUpdateEventProperties properties);
        void PublishTrainRemoved(Guid trainId);
    }
    
    /// <summary>
    /// UniRxを利用した列車イベント実装
    /// UniRx-backed implementation for train update events.
    /// </summary>
    public class TrainUpdateEvent : ITrainUpdateEvent
    {
        private readonly Subject<TrainInventoryUpdateEventProperties> _inventorySubject = new();
        private readonly Subject<Guid> _removedSubject = new();
        
        public IObservable<TrainInventoryUpdateEventProperties> OnInventoryUpdated => _inventorySubject;
        public IObservable<Guid> OnTrainRemoved => _removedSubject;
        
        public IDisposable SubscribeInventory(Action<TrainInventoryUpdateEventProperties> handler)
        {
            return _inventorySubject.Subscribe(handler);
        }
        
        public IDisposable SubscribeRemoval(Action<Guid> handler)
        {
            return _removedSubject.Subscribe(handler);
        }
        
        public void PublishInventoryUpdate(TrainInventoryUpdateEventProperties properties)
        {
            _inventorySubject.OnNext(properties);
        }
        
        public void PublishTrainRemoved(Guid trainId)
        {
            _removedSubject.OnNext(trainId);
        }
    }
}
