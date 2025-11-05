using System;
using Core.Item.Interface;
using UniRx;

namespace Game.Context.Event
{
    /// <summary>
    /// 列車インベントリ更新イベントの購読インターフェース
    /// Subscription interface for train inventory update events
    /// </summary>
    public interface ITrainInventoryUpdateEvent
    {
        IObservable<TrainInventoryUpdateEventProperties> OnUpdate { get; }
        IDisposable Subscribe(Action<TrainInventoryUpdateEventProperties> handler);
        void Publish(TrainInventoryUpdateEventProperties properties);
    }
    
    /// <summary>
    /// 列車削除イベントの購読インターフェース
    /// Subscription interface for train removal events
    /// </summary>
    public interface ITrainRemovedEvent
    {
        IObservable<Guid> OnRemoved { get; }
        IDisposable Subscribe(Action<Guid> handler);
        void Publish(Guid trainId);
    }
    
    /// <summary>
    /// 列車インベントリ更新イベントのプロパティ
    /// Event payload for train inventory updates
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
    /// 列車インベントリ更新イベントの実装
    /// Implementation for train inventory update event
    /// </summary>
    public class TrainInventoryUpdateEvent : ITrainInventoryUpdateEvent
    {
        private readonly Subject<TrainInventoryUpdateEventProperties> _subject = new();
        public IObservable<TrainInventoryUpdateEventProperties> OnUpdate => _subject;
        
        public IDisposable Subscribe(Action<TrainInventoryUpdateEventProperties> handler)
        {
            return _subject.Subscribe(handler);
        }
        
        public void Publish(TrainInventoryUpdateEventProperties properties)
        {
            _subject.OnNext(properties);
        }
    }
    
    /// <summary>
    /// 列車削除イベントの実装
    /// Implementation for train removal event
    /// </summary>
    public class TrainRemovedEvent : ITrainRemovedEvent
    {
        private readonly Subject<Guid> _subject = new();
        public IObservable<Guid> OnRemoved => _subject;
        
        public IDisposable Subscribe(Action<Guid> handler)
        {
            return _subject.Subscribe(handler);
        }
        
        public void Publish(Guid trainId)
        {
            _subject.OnNext(trainId);
        }
    }
}
