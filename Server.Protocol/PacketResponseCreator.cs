using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.PacketHandle.PacketResponse;
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
            _packetResponseList.Add(new PlayerCoordinateSendProtocol(serviceProvider.GetService<IWorldBlockDatastore>()));
            _packetResponseList.Add(new PlayerInventoryResponseProtocol(serviceProvider.GetService<IPlayerInventoryDataStore>()));
            _packetResponseList.Add(new SendEventProtocol(serviceProvider.GetService<EventProtocolProvider>()));
            _packetResponseList.Add(new BlockInventoryPlayerInventoryMoveItemProtocol(serviceProvider));
            _packetResponseList.Add(new PlayerInventoryMoveItemProtocol(serviceProvider));
            _packetResponseList.Add(new BlockInventoryMoveItemProtocol(serviceProvider));
            _packetResponseList.Add(new SendPlaceHotBarBlockProtocol(serviceProvider));
            _packetResponseList.Add(new BlockInventoryRequestProtocol(serviceProvider));
            _packetResponseList.Add(new RemoveBlockProtocol());

            serviceProvider.GetService<VeinGenerator>();
        }

        public List<byte[]> GetPacketResponse(List<byte> payload)
        {
            return _packetResponseList[new ByteArrayEnumerator(payload).MoveNextToGetShort()].GetResponse(payload);
        }
    }
}