using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Server.Util;

namespace Server.Event.EventReceive
{
    public class CraftingInventoryUpdateToSetEventPacket
    {
        private const short EventId = 4;
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public CraftingInventoryUpdateToSetEventPacket(EventProtocolProvider eventProtocolProvider,
            ICraftInventoryUpdateEvent craftInventoryUpdateEvent,IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _playerInventoryDataStore = playerInventoryDataStore;
            craftInventoryUpdateEvent.Subscribe(ReceivedEvent);
        }
        
        
        private void ReceivedEvent(PlayerInventoryUpdateEventProperties properties)
        {
            var craftInventory = _playerInventoryDataStore.GetInventoryData(properties.PlayerId);
            
            var payload = new List<byte>();

            payload.AddRange(ToByteList.Convert(ServerEventConst.EventPacketId));
            payload.AddRange(ToByteList.Convert(EventId));
            payload.AddRange(ToByteList.Convert(properties.InventorySlot));
            payload.AddRange(ToByteList.Convert(properties.ItemId));
            payload.AddRange(ToByteList.Convert(properties.Count));
            
            var creatableItem = craftInventory.CraftingOpenableInventory.GetCreatableItem();
            payload.AddRange(ToByteList.Convert(creatableItem.Id));
            payload.AddRange(ToByteList.Convert(creatableItem.Count));
            
            var isCreatable = craftInventory.CraftingOpenableInventory.IsCreatable();
            if (isCreatable)
            {
                payload.Add(1);
            }else
            {
                payload.Add(0);
            }
            

            _eventProtocolProvider.AddEvent(properties.PlayerId, payload);
        }
    }
    
    

}