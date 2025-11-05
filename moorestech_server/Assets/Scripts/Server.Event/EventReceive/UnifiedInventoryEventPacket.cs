using System;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// 統一インベントリ更新イベントパケット
    /// Unified inventory update event packet
    /// </summary>
    public class UnifiedInventoryEventPacket
    {
        public const string EventTag = "va:event:invUpdate";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IInventorySubscriptionStore _inventorySubscriptionStore;
        
        public UnifiedInventoryEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IInventorySubscriptionStore inventorySubscriptionStore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventorySubscriptionStore = inventorySubscriptionStore;
            
            // ブロックインベントリ更新イベントを購読
            // Subscribe to block inventory update event
            ServerContext.BlockOpenableInventoryUpdateEvent.Subscribe(OnBlockInventoryUpdate);
            
            // ブロック削除イベントを購読（削除通知用）
            // Subscribe to block deletion event (for removal notification)
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
            
            // TODO: 列車インベントリ更新イベントを購読
            // TODO: Subscribe to train inventory update event
            
            // TODO: 列車削除イベントを購読（削除通知用）
            // TODO: Subscribe to train deletion event (for removal notification)
        }
        
        
        #region Event Handlers
        
        /// <summary>
        /// ブロックインベントリ更新イベントハンドラー
        /// Block inventory update event handler
        /// </summary>
        private void OnBlockInventoryUpdate(BlockOpenableInventoryUpdateEventProperties properties)
        {
            // ブロック座標を取得
            // Get block position
            var pos = ServerContext.WorldBlockDatastore.GetBlockPosition(properties.BlockInstanceId);
            
            // サブスクライバーを取得
            // Get subscribers
            var playerIds = _inventorySubscriptionStore.GetSubscribers(InventoryType.Block, pos);
            if (playerIds.Count == 0) return;
            
            // イベントペイロードを構築
            // Build event payload
            var messagePack = new UnifiedInventoryEventMessagePack(
                InventoryEventType.Update,
                InventoryType.Block,
                new InventoryIdentifierMessagePack(pos),
                properties.Slot,
                properties.ItemStack);
            
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            // サブスクライバーごとにイベントを送信
            // Send event to each subscriber
            foreach (var playerId in playerIds)
            {
                _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
            }
        }
        
        /// <summary>
        /// ブロック削除イベントハンドラー
        /// Block removal event handler
        /// </summary>
        private void OnBlockRemove(Game.World.Interface.DataStore.BlockUpdateProperties properties)
        {
            // ブロック座標を取得
            // Get block position
            var pos = properties.Pos;
            
            // サブスクライバーをチェック
            // Check subscribers
            var playerIds = _inventorySubscriptionStore.GetSubscribers(InventoryType.Block, pos);
            if (playerIds.Count == 0) return;
            
            // 削除通知を送信
            // Send removal notification
            SendRemovalNotification(InventoryType.Block, pos);
        }
        
        /// <summary>
        /// インベントリ削除通知を送信（ブロック破壊、列車削除時）
        /// Send inventory removal notification (on block destroy, train deletion)
        /// </summary>
        private void SendRemovalNotification(InventoryType type, object identifier)
        {
            // サブスクライバーを取得
            // Get subscribers
            var playerIds = _inventorySubscriptionStore.GetSubscribers(type, identifier);
            if (playerIds.Count == 0) return;
            
            // イベントペイロードを構築
            // Build event payload
            var identifierMessagePack = type switch
            {
                InventoryType.Block => new InventoryIdentifierMessagePack((Vector3Int)identifier),
                InventoryType.Train => new InventoryIdentifierMessagePack((Guid)identifier),
                _ => throw new ArgumentException($"Unknown InventoryType: {type}")
            };
            
            var messagePack = new UnifiedInventoryEventMessagePack(
                InventoryEventType.Remove,
                type,
                identifierMessagePack,
                0,  // Slot is not used for removal
                null);  // Item is not used for removal
            
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            // サブスクライバーごとにイベントを送信
            // Send event to each subscriber
            foreach (var playerId in playerIds)
            {
                _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
                // サブスクリプションを解除
                // Unsubscribe
                _inventorySubscriptionStore.Unsubscribe(playerId);
            }
        }
        
        #endregion
    }
    
    
    [MessagePackObject]
    public class UnifiedInventoryEventMessagePack
    {
        [Key(0)] public string Tag { get; set; }
        [Key(1)] public InventoryEventType EventType { get; set; }
        [Key(2)] public InventoryType InventoryType { get; set; }
        [Key(3)] public InventoryIdentifierMessagePack Identifier { get; set; }
        [Key(4)] public int Slot { get; set; }
        [Key(5)] public ItemMessagePack Item { get; set; }
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public UnifiedInventoryEventMessagePack()
        {
        }
        
        public UnifiedInventoryEventMessagePack(
            InventoryEventType eventType,
            InventoryType inventoryType,
            InventoryIdentifierMessagePack identifier,
            int slot,
            IItemStack item)
        {
            Tag = UnifiedInventoryEventPacket.EventTag;
            EventType = eventType;
            InventoryType = inventoryType;
            Identifier = identifier;
            Slot = slot;
            Item = item != null ? new ItemMessagePack(item) : null;
        }
    }
}

