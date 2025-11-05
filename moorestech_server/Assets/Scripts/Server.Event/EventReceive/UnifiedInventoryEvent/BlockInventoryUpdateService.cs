using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using UniRx;
using UnityEngine;
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
            
            ServerContext.BlockOpenableInventoryUpdateEvent.OnInventoryUpdated.Subscribe(OnBlockInventoryUpdate);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }
        
        /// <summary>
        /// ブロックインベントリ更新
        /// Block inventory update event handler
        /// </summary>
        private void OnBlockInventoryUpdate(BlockOpenableInventoryUpdateEventProperties properties)
        {
            var (identifier, playerIds) = GetSubscribers(properties.BlockInstanceId);
            if (playerIds.Count == 0) return;
            
            AddEvent(identifier, playerIds);
        }
        
        /// <summary>
        /// ブロック削除
        /// Block removal event handler
        /// </summary>
        private void OnBlockRemove(Game.World.Interface.DataStore.BlockUpdateProperties properties)
        {
            var (identifier, playerIds) = GetSubscribers(properties.Pos);
            if (playerIds.Count == 0) return;
            
            AddEvent(identifier, playerIds);
        }
        
        
        (BlockInventorySubscriptionIdentifier identifier, List<int> playerIds) GetSubscribers(BlockInstanceId instanceId)
        {
            var pos = ServerContext.WorldBlockDatastore.GetBlockPosition(instanceId);
            return GetSubscribers(pos);
        }
        (BlockInventorySubscriptionIdentifier identifier, List<int> playerIds) GetSubscribers(Vector3Int position)
        {
            var id = new BlockInventorySubscriptionIdentifier(position);
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

    }
}