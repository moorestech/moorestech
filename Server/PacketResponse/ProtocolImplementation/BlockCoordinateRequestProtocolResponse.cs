using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Block;
using industrialization.OverallManagement.DataStore;
using industrialization.Server.Util;

namespace industrialization.Server.PacketResponse.ProtocolImplementation
{
    /// <summary>
    /// 設置物の座標とintIDを要求するプロトコルを受けたときにレスポンスを作成するクラス
    /// </summary>
    public static class BlockCoordinateRequestProtocolResponse
    {
        private const int DefaultChunkSize = 4;
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
            //入力さた座標をデフォルトチャンクサイズの倍数に変換する
            x = x / DefaultChunkSize * DefaultChunkSize;
            y = y / DefaultChunkSize * DefaultChunkSize;
            

            var inst = new List<BlockBase>();
            
            //データの取得
            //1チャンクのサイズ分ループを回してデータを取得する
            for (int i = x; i < x+DefaultChunkSize; i++)
            {
                for (int j = y; j < y+DefaultChunkSize; j++)
                {
                    if(!WorldBlockDatastore.ContainsCoordinate(i, j)) continue;
                    inst.Add(WorldBlockDatastore.GetBlock(i, j));   
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
                var c = WorldBlockDatastore.GetCoordinate(i.IntId);
                
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(c.x));
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(c.y));
                
                responsePayload.AddRange(ByteArrayConverter.ToByteArray(i.BlockId));
            }

            var returnPayload = new byte[1][];
            returnPayload[0] = responsePayload.ToArray();
            
            return returnPayload;
        }
    }
}