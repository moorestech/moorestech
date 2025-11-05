using System;
using Core.Item.Interface;
using Game.Block.Interface.Event;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using static Server.Util.MessagePack.InventoryIdentifierMessagePack;

namespace Server.Event.EventReceive.UnifiedInventoryEvent
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
            
            // ブロック削除イベントを購読（削除通知用）
            // Subscribe to block deletion event (for removal notification)
            
            // TODO: 列車インベントリ更新イベントを購読
            // TODO: Subscribe to train inventory update event
            
            // TODO: 列車削除イベントを購読（削除通知用）
            // TODO: Subscribe to train deletion event (for removal notification)
        }
    }
    
    
    [MessagePackObject]
    public class UnifiedInventoryEventMessagePack
    {
        [Key(0)] public string Tag { get; set; }
        [Key(1)] public InventoryEventType EventType { get; set; }
        [Key(3)] public InventoryIdentifierMessagePack Identifier { get; set; }
        [Key(4)] public int Slot { get; set; }
        [Key(5)] public ItemMessagePack Item { get; set; }
        
        
        public UnifiedInventoryEventMessagePack() { Tag = UnifiedInventoryEventPacket.EventTag; }
        
        public static UnifiedInventoryEventMessagePack CreateUpdate(ISubscriptionIdentifier identifier, int slot, IItemStack item)
        {
            return new UnifiedInventoryEventMessagePack
            {
                Tag = UnifiedInventoryEventPacket.EventTag,
                EventType = InventoryEventType.Update,
                Identifier = CreateMessagePack(identifier),
                Slot = slot,
                Item = new ItemMessagePack(item),
            };
            
        }
        
        public static UnifiedInventoryEventMessagePack CreateRemove(ISubscriptionIdentifier identifier)
        {
            return new UnifiedInventoryEventMessagePack
            {
                Tag = UnifiedInventoryEventPacket.EventTag,
                EventType = InventoryEventType.Remove,
                Identifier = CreateMessagePack(identifier),
                Slot = -1,
                Item = null,
            };
        }
        
        private static InventoryIdentifierMessagePack CreateMessagePack(ISubscriptionIdentifier identifier)
        {
            return identifier switch
            {
                BlockInventorySubscriptionIdentifier blockId => CreateBlockMessage(blockId.Position),
                TrainInventorySubscriptionIdentifier trainId => CreateTrainMessage(trainId.TrainId),
                _ => throw new ArgumentException($"Unknown ISubscriptionIdentifier type: {identifier.GetType()}")
            };
        }
    }
}
