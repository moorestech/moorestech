using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Event;
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
            trainUpdateEvent.OnTrainRemoved.Subscribe(OnTrainRemoved);
        }
        
        private void OnTrainInventoryUpdate(TrainInventoryUpdateEventProperties properties)
        {
            // サブスクライバーを取得
            // Get subscribers
            var (identifier, playerIds) = GetSubscribers(properties.TrainCarId);
            if (playerIds.Count == 0) return;
            
            var message = UnifiedInventoryEventMessagePack.CreateUpdate(identifier, properties.Slot, properties.ItemStack);
            AddEvent(message, playerIds);
        }
        
        private void OnTrainRemoved(Guid trainId)
        {
            // サブスクライバーを取得
            // Get subscribers
            var (identifier, playerIds) = GetSubscribers(trainId);
            if (playerIds.Count == 0) return;
            
            AddEvent(UnifiedInventoryEventMessagePack.CreateRemove(identifier), playerIds);
        }
        
        private (TrainInventorySubInventoryIdentifier identifier, List<int> playerIds) GetSubscribers(Guid trainId)
        {
            var id = new TrainInventorySubInventoryIdentifier(trainId);
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
