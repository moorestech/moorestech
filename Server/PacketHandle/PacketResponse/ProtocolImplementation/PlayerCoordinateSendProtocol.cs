using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Block;
using industrialization.OverallManagement.DataStore;
using industrialization.OverallManagement.Util;
using industrialization.Server.Player;
using industrialization.Server.Util;

namespace industrialization.Server.PacketHandle.PacketResponse.ProtocolImplementation
{
    /// <summary>
    /// プレイヤー座標のプロトコル
    /// </summary>
    public class PlayerCoordinateSendProtocol
    {
        static Dictionary<string,PlayerCoordinateToResponse> _responses = new Dictionary<string, PlayerCoordinateToResponse>();
        public List<byte[]> GetResponse(byte[] payload)
        {
            //プレイヤー座標の解析
            var b = new ByteArrayEnumerator(payload);
            b.MoveNextToGetShort();
            var x = b.MoveNextToGetFloat();
            var y = b.MoveNextToGetFloat();
            var name = b.MoveNextToGetString();
            //新しいプレイヤーの情報ならDictionaryに追加する
            if (!_responses.ContainsKey(name))
            {
                _responses.Add(name,new PlayerCoordinateToResponse());
            }
            
            //プレイヤーの座標から返すチャンクのブロックデータを取得をする
            //byte配列に変換して返す
            return _responses[name].
                GetResponseCoordinate(CoordinateCreator.New((int) x, (int) y)).Select(BlockToPayload).ToList();
        }

        private byte[] BlockToPayload(Coordinate chunk)
        {
            var payload = new List<bool>();
            
            payload.AddRange(ByteArrayToBitArray.Convert(ByteArrayConverter.ToByteArray((short)1)));
            payload.AddRange(ByteArrayToBitArray.Convert(ByteArrayConverter.ToByteArray(chunk.x)));
            payload.AddRange(ByteArrayToBitArray.Convert(ByteArrayConverter.ToByteArray(chunk.y)));
            var blocks = CoordinateToChunkBlocks.Convert(chunk);
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                for (int j = 0; j < blocks.GetLength(1); j++)
                {
                    var id = blocks[i, j];
                    //空気ブロックの追加
                    if (id == BlockConst.NullBlockId)
                    {
                        payload.Add(false);
                        continue;
                    }
                    
                    payload.Add(true);

                    //byte整数
                    if (-128 <= id && id <= 127)
                    {
                        payload.Add(false);
                        payload.Add(false);
                        payload.AddRange(ByteArrayToBitArray.Convert(ByteArrayConverter.ToByteArray((byte)id)));
                        continue;
                    }
                    //short整数
                    if (-32768  <= id && id <= 32767)
                    {
                        payload.Add(false);
                        payload.Add(true);
                        payload.AddRange(ByteArrayToBitArray.Convert(ByteArrayConverter.ToByteArray((short)id)));
                        continue;
                    }
                    //int整数
                    payload.Add(true);
                    payload.Add(false);
                    payload.AddRange(ByteArrayToBitArray.Convert(ByteArrayConverter.ToByteArray(id)));
                }
            }
            
            return BitArrayToByteArray.Convert(payload);
        }


        private static PlayerCoordinateSendProtocol _instance;
        public static PlayerCoordinateSendProtocol Instance
        {
            get
            {
                if (_instance is null) _instance = new PlayerCoordinateSendProtocol();
                return _instance;
            }
        }
    }
}