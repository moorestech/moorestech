using System;
using System.Collections.Generic;
using Game.Block.Interface.Event;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using UniRx;
using static Server.Event.EventReceive.UnifiedInventoryEvent.UnifiedInventoryEventPacket;

namespace Server.Event.EventReceive.UnifiedInventoryEvent
{
    /// <summary>
    /// ブロックインベントリの更新、削除を監視し、必要に応じてクライアント向けのイベントを発行するサービス
    /// Service that monitors block inventory updates and deletions, and issues client events as needed
    /// </summary>
    public class BlockInventoryUpdateService
    {
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IInventorySubscriptionStore _inventorySubscriptionStore;
        
        public BlockInventoryUpdateService(EventProtocolProvider eventProtocolProvider, IInventorySubscriptionStore inventorySubscriptionStore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventorySubscriptionStore = inventorySubscriptionStore;
            
            ServerContext.BlockOpenableInventoryUpdateEvent.Subscribe(OnBlockInventoryUpdate);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }
        
        
        /// <summary>
        /// ブロックインベントリ更新イベントハンドラー
        /// Block inventory update event handler
        /// </summary>
        private void OnBlockInventoryUpdate(BlockOpenableInventoryUpdateEventProperties properties)
        {
            // サブスクライバーを取得
            // Get subscribers
            var (identifier, playerIds) = GetSubscribers();
            if (playerIds.Count == 0) return;
            
            // サブスクライバーごとにイベントを送信
            // Send event to each subscriber
            AddEvent(identifier, playerIds);
            
            #region Internal
            
            (BlockInventorySubscriptionIdentifier identifier, List<int> playerIds) GetSubscribers()
            {
                var pos = ServerContext.WorldBlockDatastore.GetBlockPosition(properties.BlockInstanceId);
                var id = new BlockInventorySubscriptionIdentifier(pos);
                var players = _inventorySubscriptionStore.GetSubscribers(id);
                
                return (id, players);
            }
            
            void AddEvent(BlockInventorySubscriptionIdentifier id, List<int> players)
            {
                var messagePack = UnifiedInventoryEventMessagePack.CreateUpdate(id, properties.Slot, properties.ItemStack);
                var payload = MessagePackSerializer.Serialize(messagePack);
                
                foreach (var playerId in players)
                {
                    _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
                }
            }
            
            #endregion
        }
        
        /// <summary>
        /// ブロック削除イベントハンドラー
        /// Block removal event handler
        /// </summary>
        private void OnBlockRemove(Game.World.Interface.DataStore.BlockUpdateProperties properties)
        {
            // ブロック座標を取得
            // Get block position
            var (identifier, playerIds) = GetSubscribers();
            if (playerIds.Count == 0) return;
            
            // サブスクライバーごとに削除イベントを送信
            // Send removal event to each subscriber
            AddEvent(identifier, playerIds);
            
            
            #region Internal
            
            (BlockInventorySubscriptionIdentifier identifier, List<int> playerIds) GetSubscribers()
            {
                var id = new BlockInventorySubscriptionIdentifier(properties.Pos);
                var players = _inventorySubscriptionStore.GetSubscribers(id);
                
                return (id, players);
            }
            
            void AddEvent(BlockInventorySubscriptionIdentifier id, List<int> players)
            {
                var messagePack = UnifiedInventoryEventMessagePack.CreateRemove(id);
                var payload = MessagePackSerializer.Serialize(messagePack);
                
                foreach (var playerId in players)
                {
                    _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
                }
            }
            
            #endregion
            
        }
    }
}