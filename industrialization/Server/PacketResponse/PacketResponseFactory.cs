using System;
using System.Collections.Generic;
using System.Text;
using industrialization.Server.PacketResponse.Implementation;

namespace industrialization.Server.PacketResponse
{
    delegate IPacketResponse Instance(byte[] payload);
    public class PacketResponseFactory
    {
        private static List<Instance> _packetResponseList = new List<Instance>();

        private static void Init()
        {
            _packetResponseList.Add(DummyProtocol.NewInstance);
            _packetResponseList.Add(PutInstallationProtocol.NewInstance);
            _packetResponseList.Add(InstallationCoordinateRequestProtocolResponse.NewInstance);
        }
        
        public static IPacketResponse GetPacketResponse(byte[] payload)
        {
            if (_packetResponseList.Count == 0) Init();

            var id = BitConverter.ToInt16(new byte[2] {payload[0], payload[1]});

            return _packetResponseList[id](payload);
        }
    }
}