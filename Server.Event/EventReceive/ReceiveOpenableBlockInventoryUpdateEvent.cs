using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.World.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class ReceiveOpenableBlockInventoryUpdateEvent
    {
        private const short EventId = 3;
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenStateDataStore;

        public ReceiveOpenableBlockInventoryUpdateEvent(
            EventProtocolProvider eventProtocolProvider, IBlockInventoryOpenStateDataStore inventoryOpenStateDataStore,
            IBlockInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _inventoryOpenStateDataStore = inventoryOpenStateDataStore;
            blockInventoryUpdateEvent.Subscribe(InventoryUpdateEvent);
        }


        private void InventoryUpdateEvent(BlockInventoryUpdateEventProperties properties)
        {
            //そのブロックを開いているプレイヤーをリストアップ
            var playerIds = _inventoryOpenStateDataStore.GetBlockInventoryOpenPlayers(properties.Block.GetBlockId());
            if (playerIds.Count == 0) return;

            //プレイヤーごとにイベントを送信
            foreach (var id in playerIds)
            {
                _eventProtocolProvider.AddEvent(id,GetPayload(properties));
            }
        }
        
        private byte[] GetPayload(BlockInventoryUpdateEventProperties properties)
        {
            var payload = new List<byte>();

            payload.AddRange(ToByteList.Convert(ServerEventConst.EventPacketId));
            payload.AddRange(ToByteList.Convert(EventId));
            payload.AddRange(ToByteList.Convert(properties.Slot));
            payload.AddRange(ToByteList.Convert(properties.ItemStack.Id));
            payload.AddRange(ToByteList.Convert(properties.ItemStack.Count));
            payload.AddRange(ToByteList.Convert(properties.ItemStack.Count));
            
            return payload.ToArray();
        }

    }
}