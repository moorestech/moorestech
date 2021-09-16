using System.Collections.Generic;
using Server.PacketHandle.PacketResponse;
using Server.Util;

namespace Server.PacketHandle
{
    public static class PacketResponseCreator
    {
        delegate List<byte[]> Responses(byte[] payload);
        private static List<Responses> _packetResponseList = new List<Responses>();

        private static void Init()
        {
            _packetResponseList.Add(DummyProtocol.GetResponse);
            _packetResponseList.Add(PutBlockProtocol.GetResponse);
            _packetResponseList.Add(PlayerCoordinateSendProtocol.Instance.GetResponse);
            _packetResponseList.Add(InventoryContentResponseProtocol.GetResponse);
        }
        
        public static List<byte[]> GetPacketResponse(byte[] payload)
        {
            if (_packetResponseList.Count == 0) Init();

            return _packetResponseList[new ByteArrayEnumerator(payload).MoveNextToGetShort()](payload);
        }
    }
}