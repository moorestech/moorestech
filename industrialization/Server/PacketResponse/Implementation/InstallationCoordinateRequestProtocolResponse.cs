using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Installation;
using industrialization.OverallManagement.DataStore;

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
            x = BitConverter.ToInt32(new byte[4] {payload[4], payload[5], payload[6], payload[7]});
            y = BitConverter.ToInt32(new byte[4] {payload[8], payload[9], payload[10], payload[11]});
            //入力さた座標を10の倍数に変換する
            x = x / 10 * 10;
            y = y / 10 * 10;
        }
        
        
        /// <summary>
        /// レスポンスの組み立て
        /// </summary>
        /// <returns></returns>
        public byte[] GetResponse()
        {
            var payload = new List<byte>();
            //パケットIDの挿入
            short id = 1;
            BitConverter.GetBytes(id).ToList().ForEach(b => payload.Add(b));

            var inst = new List<InstallationBase>();
            
            //データの取得
            //1チャンクは10*10ブロック
            for (int i = x; i < x+10; i++)
            {
                for (int j = y; j < y+10; j++)
                {
                    var instbase = WorldInstallationDatastore.GetInstallation(x, y);
                    if (instbase != null)
                    {
                        inst.Add(instbase);   
                    }
                }
            }
            //建物データの数
            BitConverter.GetBytes(inst.Count).ToList().ForEach(b => payload.Add(b));
            //パディング
            payload.Add(0);
            payload.Add(0);
            
            //データをバイト配列に登録
            foreach (var i in inst)
            {
                var c = WorldInstallationDatastore.GetCoordinate(i.Guid);
                BitConverter.GetBytes(c.x).ToList().ForEach(b => payload.Add(b));
                BitConverter.GetBytes(c.y).ToList().ForEach(b => payload.Add(b));
                BitConverter.GetBytes(i.InstallationId).ToList().ForEach(b => payload.Add(b));
                i.Guid.ToByteArray().ToList().ForEach(b => payload.Add(b));
            }

            return payload.ToArray();
        }

        public static IPacketResponse NewInstance(byte[] payload)
        {
            return new InstallationCoordinateRequestProtocolResponse(payload);
        }
    }
}