using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
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
            var identifier = ConvertIdentifier(data.Identifier);
            if (data.IsSubscribe)
            {
                _inventorySubscriptionStore.Subscribe(data.PlayerId, identifier);
            }
            else
            {
                _inventorySubscriptionStore.Unsubscribe(data.PlayerId, identifier);
            }
            
            return null;
            
            #region Internal
            
            ISubInventoryIdentifier ConvertIdentifier(InventoryIdentifierMessagePack id)
            {
                return id.InventoryType switch
                {
                    InventoryType.Block => new BlockInventorySubInventoryIdentifier(id.BlockPosition.Vector3Int),
                    InventoryType.Train => new TrainInventorySubInventoryIdentifier(Guid.Parse(id.TrainCarId)),
                    _ => throw new ArgumentException($"Unknown InventoryType: {id.InventoryType}")
                };
            }
            
            #endregion
        }
        
        
        [MessagePackObject]
        public class SubscribeInventoryRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(4)] public InventoryIdentifierMessagePack Identifier { get; set; }
            [Key(5)] public bool IsSubscribe { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SubscribeInventoryRequestMessagePack() { }
            
            public SubscribeInventoryRequestMessagePack(int playerId, InventoryIdentifierMessagePack identifier, bool isSubscribe)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Identifier = identifier;
                IsSubscribe = isSubscribe;
            }
        }
    }
}
