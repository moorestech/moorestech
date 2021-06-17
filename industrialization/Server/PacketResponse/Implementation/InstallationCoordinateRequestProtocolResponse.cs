using System;

namespace industrialization.Server.PacketResponse.Implementation
{
    public class InstallationCoordinateRequestProtocolResponse : IPacketResponse
    {
        private int x;
        private int y;
        /// <summary>
        /// 必要なデータをインスタ時に解析する
        /// </summary>
        /// <param name="payload">パケットのペイロード</param>
        public InstallationCoordinateRequestProtocolResponse(byte[] payload)
        {
            x = BitConverter.ToInt32(new byte[4] {payload[5], payload[6], payload[7], payload[8]});
            y = BitConverter.ToInt32(new byte[4] {payload[9], payload[10], payload[11], payload[12]});
        }
        public byte[] GetResponse()
        {
            throw new System.NotImplementedException();
        }
    }
}