using System;
using System.Collections.Generic;
using System.Text;
using industrialization.Server.PacketResponse.Implementation;

namespace industrialization.Server.PacketResponse
{
    public class PacketResponseFactory
    {
        private static List<IPacketResponse> _packetResponseList;

        private static void init()
        {
            
        }
        
        public static IPacketResponse GetPacketResponse(byte[] payload)
        {
            if (_packetResponseList == null) init();

            var id = BitConverter.ToInt16(new byte[2] {payload[0], payload[1]});


            //TODO ここ書く
            return _packetResponseList[id];
        }
    }
}