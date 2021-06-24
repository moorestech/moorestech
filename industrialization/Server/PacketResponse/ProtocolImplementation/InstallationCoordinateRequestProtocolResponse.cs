using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Installation;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.Util;

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
            
            
            var responsePayload = new List<byte>();
            //パケットIDの挿入
            short id = 1;
            responsePayload.AddRange(ByteArrayConverter.ToByteArray(id));

            var inst = new List<InstallationBase>();
            
            //データの取得
            //1チャンクは10*10ブロック
            for (int i = x; i < x+10; i++)
            {
                for (int j = y; j < y+10; j++)
                {
                    var instbase = WorldInstallationDatastore.GetInstallation(x, y);
                    if (instbase.Guid.ToString() != Guid.Empty.ToString())
                    {
                        inst.Add(instbase);   
                    }
                }
            }
            //建物データの数
            responsePayload.AddRange(ByteArrayConverter.ToByteArray(inst.Count));
            //要求された座標
            responsePayload.AddRange(ByteArrayConverter.ToByteArray(x));
            responsePayload.AddRange(ByteArrayConverter.ToByteArray(y));
            //パディング
            responsePayload.Add(0);
            
            //データをバイト配列に登録
            foreach (var i in inst)
            {
                var c = WorldInstallationDatastore.GetCoordinate(i.Guid);
                
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(c.x));
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(c.y));
                
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(i.InstallationId));
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(i.Guid));
            }

            return responsePayload.ToArray();
        }
    }
}