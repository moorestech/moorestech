using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.Protocol.PacketResponse;
using Server.Util;

namespace Server.Protocol
{
    public class PacketResponseCreator
    {
        private List<IPacketResponse> _packetResponseList;
        
        //この辺もDIコンテナに載せる?
        public PacketResponseCreator(ServiceProvider serviceProvider)
        {
            _packetResponseList = new List<IPacketResponse>();
            _packetResponseList.Add(new DummyProtocol());
            _packetResponseList.Add(new PutBlockProtocol(serviceProvider));
            _packetResponseList.Add(new PlayerCoordinateSendProtocol(serviceProvider));
            _packetResponseList.Add(new PlayerInventoryResponseProtocol(serviceProvider.GetService<IPlayerInventoryDataStore>()));
            _packetResponseList.Add(new EventProtocol(serviceProvider.GetService<EventProtocolProvider>()));
            _packetResponseList.Add(new InventoryItemMoveProtocol(serviceProvider));
            _packetResponseList.Add(new DummyProtocol());
            _packetResponseList.Add(new DummyProtocol());
            _packetResponseList.Add(new SendPlaceHotBarBlockProtocol(serviceProvider));
            _packetResponseList.Add(new BlockInventoryRequestProtocol(serviceProvider));
            _packetResponseList.Add(new RemoveBlockProtocol(serviceProvider));
            _packetResponseList.Add(new SendCommandProtocol(serviceProvider));
            _packetResponseList.Add(new DummyProtocol());
            _packetResponseList.Add(new DummyProtocol());
            _packetResponseList.Add(new CraftProtocol(serviceProvider));
            _packetResponseList.Add(new MiningOperationProtocol(serviceProvider)); 
            _packetResponseList.Add(new BlockInventoryOpenCloseProtocol(serviceProvider));
            _packetResponseList.Add(new SaveProtocol(serviceProvider));

            serviceProvider.GetService<VeinGenerator>();
        }

        public List<byte[]> GetPacketResponse(List<byte> payload)
        {
            return _packetResponseList[new ByteListEnumerator(payload).MoveNextToGetShort()].GetResponse(payload);
        }
    }
}