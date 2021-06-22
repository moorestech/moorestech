using System;
using System.Collections.Generic;
using System.Text;
using industrialization.Server.PacketResponse.ProtocolImplementation;

namespace industrialization.Server.PacketResponse
{
    delegate byte[] Responses(byte[] payload);
    public static class PacketResponseFactory
    {
        private static List<Responses> _packetResponseList = new List<Responses>();

        private static void Init()
        {
            _packetResponseList.Add(DummyProtocol.GetResponse);
            _packetResponseList.Add(PutInstallationProtocol.GetResponse);
            _packetResponseList.Add(InstallationCoordinateRequestProtocolResponse.GetResponse);
            _packetResponseList.Add(InventoryContentResponseProtocol.GetResponse);
        }
        
        public static byte[] GetPacketResponse(byte[] payload)
        {
            if (_packetResponseList.Count == 0) Init();

            var id = BitConverter.ToInt16(new byte[2] {payload[0], payload[1]});

            return _packetResponseList[id](payload);
        }
    }
}