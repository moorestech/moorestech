using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.Block.Interface.Event;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    public class OpenableBlockInventoryUpdateToSetEventPacket
    {
        public const string EventTag = "va:event:blockInvUpdate";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenStateDataStore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        private DateTime now = DateTime.Now;

        public OpenableBlockInventoryUpdateToSetEventPacket(
            EventProtocolProvider eventProtocolProvider, IBlockInventoryOpenStateDataStore inventoryOpenStateDataStore,
            IBlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent, IWorldBlockDatastore worldBlockDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventoryOpenStateDataStore = inventoryOpenStateDataStore;
            _worldBlockDatastore = worldBlockDatastore;
            blockInventoryUpdateEvent.Subscribe(InventoryUpdateEvent);
        }


        private void InventoryUpdateEvent(BlockOpenableInventoryUpdateEventProperties properties)
        {
            
            var playerIds = _inventoryOpenStateDataStore.GetBlockInventoryOpenPlayers(properties.EntityId);
            if (playerIds.Count == 0) return;

            
            foreach (var id in playerIds) _eventProtocolProvider.AddEvent(id, GetPayload(properties));
        }

        private List<byte> GetPayload(BlockOpenableInventoryUpdateEventProperties properties)
        {
            var (x, y) = _worldBlockDatastore.GetBlockPosition(properties.EntityId);

            return MessagePackSerializer.Serialize(new OpenableBlockInventoryUpdateEventMessagePack(
                x, y, properties.Slot, properties.ItemStack
            )).ToList();
        }
    }


    [MessagePackObject(true)]
    public class OpenableBlockInventoryUpdateEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public OpenableBlockInventoryUpdateEventMessagePack()
        {
        }

        public OpenableBlockInventoryUpdateEventMessagePack(int x, int y, int slot, IItemStack item)
        {
            EventTag = OpenableBlockInventoryUpdateToSetEventPacket.EventTag;
            X = x;
            Y = y;
            Slot = slot;
            Item = new ItemMessagePack(item.Id, item.Count);
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Slot { get; set; }
        public ItemMessagePack Item { get; set; }
    }
}