using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface.Subscription;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

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
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SubscribeInventoryRequestMessagePack>(payload);
            
            // サブスクライブまたはアンサブスクライブを実行
            // Execute subscribe or unsubscribe
            var identifier = ConvertIdentifier(data.Identifier);
            if (data.IsSubscribe)
            {
                _inventorySubscriptionStore.Subscribe(context.PlayerId.Value, identifier);
            }
            else
            {
                _inventorySubscriptionStore.Unsubscribe(context.PlayerId.Value, identifier);
            }
            
            return null;
            
            #region Internal
            
            ISubInventoryIdentifier ConvertIdentifier(InventoryIdentifierMessagePack id)
            {
                return id.InventoryType switch
                {
                    InventoryType.Block => new BlockInventorySubInventoryIdentifier(id.BlockPosition.Vector3Int),
                    InventoryType.Train => new TrainInventorySubInventoryIdentifier(long.Parse(id.TrainCarInstanceId)),
                    _ => throw new ArgumentException($"Unknown InventoryType: {id.InventoryType}")
                };
            }
            
            #endregion
        }
        
        
        [MessagePackObject]
        public class SubscribeInventoryRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public InventoryIdentifierMessagePack Identifier { get; set; }
            [Key(3)] public bool IsSubscribe { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SubscribeInventoryRequestMessagePack() { }
            
            public SubscribeInventoryRequestMessagePack(InventoryIdentifierMessagePack identifier, bool isSubscribe)
            {
                Tag = ProtocolTag;
                Identifier = identifier;
                IsSubscribe = isSubscribe;
            }
        }
    }
}
