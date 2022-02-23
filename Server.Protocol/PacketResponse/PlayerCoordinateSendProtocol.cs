using System;
using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using Server.PacketHandle.PacketResponse.Player;
using Server.Protocol.PacketResponse.Player;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// プレイヤー座標のプロトコル
    /// </summary>
    public class PlayerCoordinateSendProtocol : IPacketResponse
    {
        private readonly Dictionary<int, PlayerCoordinateToResponse> _responses = new();
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public PlayerCoordinateSendProtocol(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            //プレイヤー座標の解析
            var b = new ByteListEnumerator(payload);
            b.MoveNextToGetShort();
            var x = b.MoveNextToGetFloat();
            var y = b.MoveNextToGetFloat();
            var playerId = b.MoveNextToGetInt();
            //新しいプレイヤーの情報ならDictionaryに追加する
            if (!_responses.ContainsKey(playerId))
            {
                _responses.Add(playerId, new PlayerCoordinateToResponse());
                Console.WriteLine("プレイヤーが接続:" + playerId);
            }

            //プレイヤーの座標から返すチャンクのブロックデータを取得をする
            //byte配列に変換して返す
            return _responses[playerId].GetResponseCoordinate(new Coordinate((int) x, (int) y)).Select(c =>
                ChunkBlockToPayload.Convert(CoordinateToChunkBlocks.Convert(c, _worldBlockDatastore), c)).ToList();
        }
    }
}