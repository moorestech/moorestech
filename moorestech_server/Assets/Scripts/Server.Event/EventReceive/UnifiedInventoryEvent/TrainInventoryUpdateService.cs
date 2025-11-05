using System;
using System.Collections.Generic;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Context.Event;
using MessagePack;

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
        
        public TrainInventoryUpdateService(EventProtocolProvider eventProtocolProvider, IInventorySubscriptionStore inventorySubscriptionStore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventorySubscriptionStore = inventorySubscriptionStore;
            
            ServerContext.TrainInventoryUpdateEvent.Subscribe(OnTrainInventoryUpdate);
            ServerContext.TrainRemovedEvent.Subscribe(OnTrainRemoved);
        }
        
        private void OnTrainInventoryUpdate(TrainInventoryUpdateEventProperties properties)
        {
            // サブスクライバーを取得
            // Get subscribers
            var (identifier, playerIds) = GetSubscribers();
            if (playerIds.Count == 0) return;
            
            // サブスクライバーへインベントリ更新を送信
            // Send inventory update to subscribers
            AddEvent(identifier, playerIds);
            
            #region Internal
            
            (TrainInventorySubscriptionIdentifier identifier, List<int> playerIds) GetSubscribers()
            {
                var id = new TrainInventorySubscriptionIdentifier(properties.TrainId);
                var players = _inventorySubscriptionStore.GetSubscribers(id);
                return (id, players);
            }
            
            void AddEvent(TrainInventorySubscriptionIdentifier id, List<int> players)
            {
                var messagePack = UnifiedInventoryEventMessagePack.CreateUpdate(id, properties.Slot, properties.ItemStack);
                var payload = MessagePackSerializer.Serialize(messagePack);
                foreach (var playerId in players)
                {
                    _eventProtocolProvider.AddEvent(playerId, UnifiedInventoryEventPacket.EventTag, payload);
                }
            }
            
            #endregion
        }
        
        private void OnTrainRemoved(Guid trainId)
        {
            // サブスクライバーを取得
            // Get subscribers
            var (identifier, playerIds) = GetSubscribers();
            if (playerIds.Count == 0) return;
            
            // サブスクライバーへ削除通知を送信
            // Send removal notification to subscribers
            AddEvent(identifier, playerIds);
            
            #region Internal
            
            (TrainInventorySubscriptionIdentifier identifier, List<int> playerIds) GetSubscribers()
            {
                var id = new TrainInventorySubscriptionIdentifier(trainId);
                var players = _inventorySubscriptionStore.GetSubscribers(id);
                return (id, players);
            }
            
            void AddEvent(TrainInventorySubscriptionIdentifier id, List<int> players)
            {
                var messagePack = UnifiedInventoryEventMessagePack.CreateRemove(id);
                var payload = MessagePackSerializer.Serialize(messagePack);
                foreach (var playerId in players)
                {
                    _eventProtocolProvider.AddEvent(playerId, UnifiedInventoryEventPacket.EventTag, payload);
                }
            }
            
            #endregion
        }
    }
}
