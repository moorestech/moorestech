using System.Collections.Generic;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Event;
using Game.Train.Unit;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive.UnifiedInventoryEvent
{
    /// <summary>
    /// 列車インベントリ更新および削除イベントを監視しクライアントへ通知
    /// Service that monitors train inventory updates and removals then notifies clients
    /// </summary>
    public class TrainInventoryUpdateService
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IInventorySubscriptionStore _inventorySubscriptionStore;
        
        public TrainInventoryUpdateService(
            EventProtocolProvider eventProtocolProvider,
            IInventorySubscriptionStore inventorySubscriptionStore,
            ITrainUpdateEvent trainUpdateEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventorySubscriptionStore = inventorySubscriptionStore;
            
            trainUpdateEvent.OnInventoryUpdated.Subscribe(OnTrainInventoryUpdate);
            trainUpdateEvent.OnTrainCarRemoved.Subscribe(OnTrainCarRemoved);
        }
        
        private void OnTrainInventoryUpdate(TrainInventoryUpdateEventProperties properties)
        {
            // サブスクライバーを取得
            // Get subscribers
            var (identifier, playerIds) = GetSubscribers(properties.TrainCarInstanceId);
            if (playerIds.Count == 0) return;
            
            var message = UnifiedInventoryEventMessagePack.CreateUpdate(identifier, properties.Slot, properties.ItemStack);
            AddEvent(message, playerIds);
        }
        
        private void OnTrainCarRemoved(TrainCarInstanceId trainCarInstanceId)
        {
            // サブスクライバーを取得
            // Get subscribers
            var (identifier, playerIds) = GetSubscribers(trainCarInstanceId);
            if (playerIds.Count == 0) return;
            
            AddEvent(UnifiedInventoryEventMessagePack.CreateRemove(identifier), playerIds);
        }
        
        private (TrainInventorySubInventoryIdentifier identifier, List<int> playerIds) GetSubscribers(TrainCarInstanceId trainCarInstanceId)
        {
            var id = new TrainInventorySubInventoryIdentifier(trainCarInstanceId.AsPrimitive());
            var players = _inventorySubscriptionStore.GetSubscribers(id);
            return (id, players);
        }
        
        private void AddEvent(UnifiedInventoryEventMessagePack messagePack, List<int> players)
        {
            var payload = MessagePackSerializer.Serialize(messagePack);
            foreach (var playerId in players)
            {
                _eventProtocolProvider.AddEvent(playerId, UnifiedInventoryEventPacket.EventTag, payload);
            }
        }
        
    }
}
