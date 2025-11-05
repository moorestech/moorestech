using System;
using Core.Item.Interface;

namespace Game.Context.Event
{
    /// <summary>
    /// 列車インベントリ更新イベントの購読インターフェース
    /// Subscription interface for train inventory update events
    /// </summary>
    public interface ITrainInventoryUpdateEvent
    {
        void Subscribe(Action<TrainInventoryUpdateEventProperties> trainInventoryEvent);
    }
    
    /// <summary>
    /// 列車削除イベントの購読インターフェース
    /// Subscription interface for train removal events
    /// </summary>
    public interface ITrainRemovedEvent
    {
        void Subscribe(Action<Guid> onRemoved);
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
        public event Action<TrainInventoryUpdateEventProperties> OnTrainInventoryUpdate;
        
        public void Subscribe(Action<TrainInventoryUpdateEventProperties> trainInventoryEvent)
        {
            OnTrainInventoryUpdate += trainInventoryEvent;
        }
        
        public void OnInventoryUpdateInvoke(TrainInventoryUpdateEventProperties properties)
        {
            OnTrainInventoryUpdate?.Invoke(properties);
        }
    }
    
    /// <summary>
    /// 列車削除イベントの実装
    /// Implementation for train removal event
    /// </summary>
    public class TrainRemovedEvent : ITrainRemovedEvent
    {
        public event Action<Guid> OnTrainRemoved;
        
        public void Subscribe(Action<Guid> onRemoved)
        {
            OnTrainRemoved += onRemoved;
        }
        
        public void OnTrainRemovedInvoke(Guid trainId)
        {
            OnTrainRemoved?.Invoke(trainId);
        }
    }
}
