using System;
using System.Collections.Generic;

namespace industrialization.Server.PacketResponse.Implementation
{
    /// <summary>
    /// 設置物の座標とGUIDを要求するプロトコルを受けたときにレスポンスを作成するクラス
    /// </summary>
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
        
        
        /// <summary>
        /// レスポンスの組み立て
        /// </summary>
        /// <returns></returns>
        public byte[] GetResponse()
        {
            var data = new List<byte>();
            //IDの挿入
            data.Add(1);
            
            //データの取得
            throw new System.NotImplementedException();
        }

        public IPacketResponse NewInstance(byte[] payload)
        {
            return new InstallationCoordinateRequestProtocolResponse(payload);
        }
    }
}