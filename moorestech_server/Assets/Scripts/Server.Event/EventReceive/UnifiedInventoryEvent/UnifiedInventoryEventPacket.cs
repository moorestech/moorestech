using System;
using Core.Item.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Util.MessagePack;
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
        
        public UnifiedInventoryEventPacket(EventProtocolProvider eventProtocolProvider, IInventorySubscriptionStore inventorySubscriptionStore)
        {
            // ブロックインベントリの更新を監視
            // Monitor block inventory updates
            new BlockInventoryUpdateService(eventProtocolProvider, inventorySubscriptionStore);
            
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
                TrainInventorySubscriptionIdentifier trainId => CreateTrainMessage(trainId.TrainCarId),
                _ => throw new ArgumentException($"Unknown ISubscriptionIdentifier type: {identifier.GetType()}")
            };
        }
    }
}
