using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Installation;
using industrialization.OverallManagement.DataStore;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    /// <summary>
    /// 設置物の座標とGUIDを要求するプロトコルを受けたときにレスポンスを作成するクラス
    /// </summary>
    public static class InstallationCoordinateRequestProtocolResponse
    {
        /// <summary>
        /// レスポンスの組み立て
        /// </summary>
        /// <returns></returns>
        public static byte[] GetResponse(byte[] payload)
        {
            int x = BitConverter.ToInt32(new byte[4] {payload[4], payload[5], payload[6], payload[7]});
            int y = BitConverter.ToInt32(new byte[4] {payload[8], payload[9], payload[10], payload[11]});
            //入力さた座標を10の倍数に変換する
            x = x / 10 * 10;
            y = y / 10 * 10;
            
            
            var responsePayload = new List<byte>();
            //パケットIDの挿入
            short id = 1;
            BitConverter.GetBytes(id).ToList().ForEach(b => responsePayload.Add(b));

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
            BitConverter.GetBytes(inst.Count).ToList().ForEach(b => responsePayload.Add(b));
            //パディング
            responsePayload.Add(0);
            responsePayload.Add(0);
            
            //データをバイト配列に登録
            foreach (var i in inst)
            {
                var c = WorldInstallationDatastore.GetCoordinate(i.Guid);
                BitConverter.GetBytes(c.x).ToList().ForEach(b => responsePayload.Add(b));
                BitConverter.GetBytes(c.y).ToList().ForEach(b => responsePayload.Add(b));
                BitConverter.GetBytes(i.InstallationId).ToList().ForEach(b => responsePayload.Add(b));
                i.Guid.ToByteArray().ToList().ForEach(b => responsePayload.Add(b));
            }

            return responsePayload.ToArray();
        }
    }
}