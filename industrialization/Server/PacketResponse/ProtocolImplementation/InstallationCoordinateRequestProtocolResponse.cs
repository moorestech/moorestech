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
        public static byte[] GetResponse(byte[] payloada)
        {
            int x = BitConverter.ToInt32(new byte[4] {payloada[4], payloada[5], payloada[6], payloada[7]});
            int y = BitConverter.ToInt32(new byte[4] {payloada[8], payloada[9], payloada[10], payloada[11]});
            //入力さた座標を10の倍数に変換する
            x = x / 10 * 10;
            y = y / 10 * 10;
            
            
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
    }
}