using System;
using System.Collections.Generic;
using System.Text;
using industrialization.Server.PacketResponse.Implementation;

namespace industrialization.Server.PacketResponse
{
    public class PacketResponseFactory
    {
        private static List<IPacketResponse> _packetResponseList;

        private static void Init()
        {
            var empty = System.Array.Empty<byte>();
            _packetResponseList.Add(new InstallationCoordinateRequestProtocolResponse(empty));
        }
        
        public static IPacketResponse GetPacketResponse(byte[] payload)
        {
            if (_packetResponseList == null) Init();

            var id = BitConverter.ToInt16(new byte[2] {payload[0], payload[1]});

            return _packetResponseList[id].NewInstance(payload);
        }
    }
}