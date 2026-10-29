using System.Collections.Generic;
using System.Linq;
using Core.Master;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Util.MessagePack;
using UnityEngine;

namespace Tests.CombinedTest.Game.InventoryMove
{
    // 移動拒否の訂正イベントを捕捉列から取り出して照合する
    // Extracts and checks move-rejection correction events from a captured event list
    public static class InventoryMoveCorrectionEvents
    {
        public static List<EventMessagePack> SlotUpdateEvents(List<EventMessagePack> events)
        {
            var slotTags = new[] { MainInventoryUpdateEventPacket.EventTag, GrabInventoryUpdateEventPacket.EventTag, UnifiedInventoryEventPacket.EventTag };
            return events.Where(e => slotTags.Contains(e.Tag)).ToList();
        }

        public static void AssertMainUpdate(List<EventMessagePack> events, int slot, ItemId itemId, int count)
        {
            var updates = events.Where(e => e.Tag == MainInventoryUpdateEventPacket.EventTag).Select(e => MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(e.Payload)).Where(m => m.Slot == slot).ToList();
            Assert.AreEqual(1, updates.Count, $"持物slot{slot}の訂正は1件");
            Assert.AreEqual(itemId, updates[0].Item.Id);
            Assert.AreEqual(count, updates[0].Item.Count);
        }

        public static void AssertBlockUpdate(List<EventMessagePack> events, Vector3Int blockPosition, int slot, ItemId itemId, int count)
        {
            var updates = events.Where(e => e.Tag == UnifiedInventoryEventPacket.EventTag).Select(e => MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(e.Payload)).Where(m => m.Slot == slot).ToList();
            Assert.AreEqual(1, updates.Count, $"ブロックslot{slot}の訂正は1件");
            Assert.AreEqual(InventoryType.Block, updates[0].Identifier.InventoryType);
            Assert.AreEqual(blockPosition, updates[0].Identifier.BlockPosition.Vector3Int);
            Assert.AreEqual(itemId, updates[0].Item.Id);
            Assert.AreEqual(count, updates[0].Item.Count);
        }
    }
}
