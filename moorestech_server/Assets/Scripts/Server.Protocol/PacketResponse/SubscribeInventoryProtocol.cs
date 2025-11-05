using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// インベントリサブスクリプションの統一プロトコル
    /// Unified protocol for inventory subscription
    /// </summary>
    public class SubscribeInventoryProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:invSubscribe";
        private readonly IInventorySubscriptionStore _inventorySubscriptionStore;
        
        public SubscribeInventoryProtocol(ServiceProvider serviceProvider)
        {
            _inventorySubscriptionStore = serviceProvider.GetService<IInventorySubscriptionStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SubscribeInventoryRequestMessagePack>(payload.ToArray());
            
            // サブスクライブまたはアンサブスクライブを実行
            // Execute subscribe or unsubscribe
            if (data.IsSubscribe)
            {
                // インベントリが存在するかチェック
                // Check if inventory exists
                if (!ValidateInventoryExists(data.Type, data.Identifier))
                {
                    return null;
                }
                
                // サブスクライブ
                // Subscribe
                var identifier = ConvertIdentifier(data.Type, data.Identifier);
                _inventorySubscriptionStore.Subscribe(data.PlayerId, identifier);
            }
            else
            {
                // アンサブスクライブ
                // Unsubscribe
                _inventorySubscriptionStore.Unsubscribe(data.PlayerId);
            }
            
            return null;
        }
        
        
        #region Internal
        
        /// <summary>
        /// インベントリが存在するかチェック
        /// Check if inventory exists
        /// </summary>
        private bool ValidateInventoryExists(InventoryType type, InventoryIdentifierMessagePack identifier)
        {
            return type switch
            {
                InventoryType.Block => ValidateBlockInventoryExists(identifier.BlockPosition),
                InventoryType.Train => ValidateTrainInventoryExists(identifier.TrainId),
                _ => false
            };
        }
        
        private bool ValidateBlockInventoryExists(Vector3IntMessagePack blockPos)
        {
            if (blockPos == null) return false;
            return ServerContext.WorldBlockDatastore.TryGetBlock<IOpenableBlockInventoryComponent>(blockPos, out _);
        }
        
        private bool ValidateTrainInventoryExists(string trainIdStr)
        {
            // TODO: 列車インベントリの存在チェックを実装
            // TODO: Implement train inventory existence check
            if (string.IsNullOrEmpty(trainIdStr)) return false;
            if (!Guid.TryParse(trainIdStr, out var trainId)) return false;
            
            // 現時点では常にtrueを返す（列車システムの実装に依存）
            // For now, always return true (depends on train system implementation)
            return true;
        }
        
        /// <summary>
        /// MessagePackの識別子を内部形式に変換
        /// Convert MessagePack identifier to internal format
        /// </summary>
        private ISubscriptionIdentifier ConvertIdentifier(InventoryType type, InventoryIdentifierMessagePack identifier)
        {
            return type switch
            {
                InventoryType.Block => new BlockInventorySubscriptionIdentifier(identifier.BlockPosition.Vector3Int),
                InventoryType.Train => new TrainInventorySubscriptionIdentifier(Guid.Parse(identifier.TrainId)),
                _ => throw new ArgumentException($"Unknown InventoryType: {type}")
            };
        }
        
        #endregion
        
        
        [MessagePackObject]
        public class SubscribeInventoryRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public InventoryType Type { get; set; }
            [Key(4)] public InventoryIdentifierMessagePack Identifier { get; set; }
            [Key(5)] public bool IsSubscribe { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SubscribeInventoryRequestMessagePack()
            {
            }
            
            public SubscribeInventoryRequestMessagePack(int playerId, InventoryType type, InventoryIdentifierMessagePack identifier, bool isSubscribe)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Type = type;
                Identifier = identifier;
                IsSubscribe = isSubscribe;
            }
        }
    }
}
