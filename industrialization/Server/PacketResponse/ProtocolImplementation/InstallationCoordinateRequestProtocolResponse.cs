using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Installation;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.Util;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    /// <summary>
    /// 設置物の座標とintIDを要求するプロトコルを受けたときにレスポンスを作成するクラス
    /// </summary>
    public static class InstallationCoordinateRequestProtocolResponse
    {
        /// <summary>
        /// レスポンスの組み立て
        /// </summary>
        /// <returns></returns>
        public static byte[][] GetResponse(byte[] payload)
        {
            var payloadData = new ByteArrayEnumerator(payload);
            //IDの取得
            payloadData.MoveNextToGetShort();
            //パディング
            payloadData.MoveNextToGetShort();
            int x = payloadData.MoveNextToGetInt();
            int y = payloadData.MoveNextToGetInt();
            //入力さた座標を10の倍数に変換する
            x = x / 10 * 10;
            y = y / 10 * 10;
            

            var inst = new List<InstallationBase>();
            
            //TODO ここ複数パケットに対応させる
            //データの取得
            //1チャンクは10*10ブロック
            for (int i = x; i < x+10; i++)
            {
                for (int j = y; j < y+10; j++)
                {
                    if(!WorldInstallationDatastore.ContainsCoordinate(i, j)) continue;
                    inst.Add(WorldInstallationDatastore.GetInstallation(i, j));   
                }
            }
            var responsePayload = new List<byte>();
            //パケットIDの挿入
            responsePayload.AddRange(ByteArrayConverter.ToByteArray((short)1));
            //建物データの数
            responsePayload.AddRange(ByteArrayConverter.ToByteArray(inst.Count));
            //パディング
            responsePayload.AddRange(ByteArrayConverter.ToByteArray((short)0));
            //要求された座標
            responsePayload.AddRange(ByteArrayConverter.ToByteArray(x));
            responsePayload.AddRange(ByteArrayConverter.ToByteArray(y));
            
            //データをバイト配列に登録
            foreach (var i in inst)
            {
                var c = WorldInstallationDatastore.GetCoordinate(i.IntId);
                
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(c.x));
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(c.y));
                
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(i.InstallationId));
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(i.IntId));
            }

            var returnPayload = new byte[1][];
            returnPayload[0] = responsePayload.ToArray();
            
            return returnPayload;
        }
    }
}