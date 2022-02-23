using System.Collections.Generic;
using Core.Block.Event;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class ReceiveOpenableBlockInventoryUpdateEvent
    {
        private const short EventId = 2;
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenStateDataStore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public ReceiveOpenableBlockInventoryUpdateEvent(
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
            //そのブロックを開いているプレイヤーをリストアップ
            var playerIds = _inventoryOpenStateDataStore.GetBlockInventoryOpenPlayers(properties.EntityId);
            if (playerIds.Count == 0) return;

            //プレイヤーごとにイベントを送信
            foreach (var id in playerIds)
            {
                _eventProtocolProvider.AddEvent(id,GetPayload(properties));
            }
        }
        
        private byte[] GetPayload(BlockOpenableInventoryUpdateEventProperties properties)
        {
            var (x, y) = _worldBlockDatastore.GetBlockPosition(properties.EntityId);
            
            var payload = new List<byte>();

            payload.AddRange(ToByteList.Convert(ServerEventConst.EventPacketId));
            payload.AddRange(ToByteList.Convert(EventId));
            payload.AddRange(ToByteList.Convert(properties.Slot));
            payload.AddRange(ToByteList.Convert(properties.ItemStack.Id));
            payload.AddRange(ToByteList.Convert(properties.ItemStack.Count));
            payload.AddRange(ToByteList.Convert(x));
            payload.AddRange(ToByteList.Convert(y));
            
            return payload.ToArray();
        }

    }
}