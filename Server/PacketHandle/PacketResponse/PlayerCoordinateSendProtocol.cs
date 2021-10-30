using System.Collections.Generic;
using System.Linq;
using Server.PacketHandle.PacketResponse.Player;
using Server.Util;
using World.DataStore;
using World.Util;

namespace Server.PacketHandle.PacketResponse
{
    /// <summary>
    /// プレイヤー座標のプロトコル
    /// </summary>
    public class PlayerCoordinateSendProtocol : IPacketResponse
    {
        private readonly Dictionary<int,PlayerCoordinateToResponse> _responses = new ();
        private readonly WorldBlockDatastore _worldBlockDatastore;

        public PlayerCoordinateSendProtocol(WorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public List<byte[]> GetResponse(byte[] payload)
        {
            //プレイヤー座標の解析
            var b = new ByteArrayEnumerator(payload);
            b.MoveNextToGetShort();
            var x = b.MoveNextToGetFloat();
            var y = b.MoveNextToGetFloat();
            var playerId = b.MoveNextToGetInt();
            //新しいプレイヤーの情報ならDictionaryに追加する
            if (!_responses.ContainsKey(playerId))
            {
                _responses.Add(playerId,new PlayerCoordinateToResponse());
            }
            
            //プレイヤーの座標から返すチャンクのブロックデータを取得をする
            //byte配列に変換して返す
            return _responses[playerId].
                GetResponseCoordinate(CoordinateCreator.New((int) x, (int) y)).
                Select(c => ChunkBlockToPayload.Convert(CoordinateToChunkBlocks.Convert(c,_worldBlockDatastore),c)).
                ToList();
        }
    }
}