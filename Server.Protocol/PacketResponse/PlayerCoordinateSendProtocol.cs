using System;
using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Player;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// プレイヤー座標のプロトコル
    /// </summary>
    public class PlayerCoordinateSendProtocol : IPacketResponse
    {
        public const string Tag = "va:playerCoordinate";
        
        private readonly Dictionary<int, PlayerCoordinateToResponse> _responses = new();
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly WorldMapTile _worldMapTile;

        public PlayerCoordinateSendProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _worldMapTile = serviceProvider.GetService<WorldMapTile>();
        }
        
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<PlayerCoordinateSendProtocolMessagePack>(payload.ToArray());
            
            //新しいプレイヤーの情報ならDictionaryに追加する
            if (!_responses.ContainsKey(data.PlayerId))
            {
                _responses.Add(data.PlayerId, new PlayerCoordinateToResponse());
                Console.WriteLine("プレイヤーが接続:" + data.PlayerId);
            }

            
            
            
            
            //プレイヤーの座標から返すチャンクのブロックデータを取得をする
            //byte配列に変換して返す

            var responseChunk = new List<List<byte>>();
            
            var responseChunkCoordinates = _responses[data.PlayerId].GetResponseChunkCoordinates(new Coordinate(data.X, data.Y));
            foreach (var chunkCoordinate in responseChunkCoordinates)
            {
                //チャンクのブロックデータを取得してバイト配列に変換する
                responseChunk.Add(ChunkBlockToPayload.Convert(chunkCoordinate,_worldBlockDatastore,_worldMapTile));
            }

            return responseChunk;
        }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class PlayerCoordinateSendProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}