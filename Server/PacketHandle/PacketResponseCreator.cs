using System.Collections.Generic;
using Server.Event;
using Server.PacketHandle.PacketResponse;
using Server.Util;
using World;

namespace Server.PacketHandle
{
    public class PacketResponseCreator
    {
        private List<IPacketResponse> _packetResponseList;


        public PacketResponseCreator(WorldBlockDatastore worldBlockDatastore,EventProtocolQueProvider eventProtocolQueProvider)
        {
            _packetResponseList = new List<IPacketResponse>();
            _packetResponseList.Add(new DummyProtocol());
            _packetResponseList.Add(new PutBlockProtocol(worldBlockDatastore));
            _packetResponseList.Add(new PlayerCoordinateSendProtocol(worldBlockDatastore));
            _packetResponseList.Add(new InventoryContentResponseProtocol());
            _packetResponseList.Add(new SendEventProtocol(eventProtocolQueProvider));
        }

        public List<byte[]> GetPacketResponse(List<byte> payload)
        {
            return _packetResponseList[new ByteArrayEnumerator(payload).MoveNextToGetShort()].GetResponse(payload);
        }
    }
}