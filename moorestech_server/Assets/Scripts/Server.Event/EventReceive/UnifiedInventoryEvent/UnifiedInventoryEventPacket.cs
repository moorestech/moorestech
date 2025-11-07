using System;
using Core.Item.Interface;
using Game.Block.Interface.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Event;
using Game.World.Interface.DataStore;
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
        
        public UnifiedInventoryEventPacket(EventProtocolProvider eventProtocolProvider, IInventorySubscriptionStore inventorySubscriptionStore, ITrainUpdateEvent trainUpdateEvent)
        {
            // ブロックインベントリの更新を監視
            // Monitor block inventory updates
            new BlockInventoryUpdateService(eventProtocolProvider, inventorySubscriptionStore);
            // 列車インベントリの更新・削除を監視
            // Monitor train inventory updates and removals
            new TrainInventoryUpdateService(eventProtocolProvider, inventorySubscriptionStore, trainUpdateEvent);
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
        
        public static UnifiedInventoryEventMessagePack CreateUpdate(ISubInventoryIdentifier identifier, int slot, IItemStack item)
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
        
        public static UnifiedInventoryEventMessagePack CreateRemove(ISubInventoryIdentifier identifier)
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
        
        private static InventoryIdentifierMessagePack CreateMessagePack(ISubInventoryIdentifier identifier)
        {
            return identifier switch
            {
                BlockInventorySubInventoryIdentifier blockId => CreateBlockMessage(blockId.Position),
                TrainInventorySubInventoryIdentifier trainId => CreateTrainMessage(trainId.TrainCarId),
                _ => throw new ArgumentException($"Unknown ISubscriptionIdentifier type: {identifier.GetType()}")
            };
        }
    }
}
