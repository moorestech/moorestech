using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.Config;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.MessagePack;
using Server.Protocol.PacketResponse.Player;
using Server.Protocol.PacketResponse.Util;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// プレイヤー座標のプロトコル
    /// </summary>
    public class PlayerCoordinateSendProtocol : IPacketResponse
    {
        public const string Tag = "va:playerCoordinate";
        public const string ChunkDataTag = "va:chunkData";
        public const string EntityDataTag = "va:entityData";
        
        private readonly Dictionary<int, PlayerCoordinateToResponse> _responses = new();
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly WorldMapTile _worldMapTile;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly IEntityFactory _entityFactory;
        
        public PlayerCoordinateSendProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _worldMapTile = serviceProvider.GetService<WorldMapTile>();
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
        }
        
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<PlayerCoordinateSendProtocolMessagePack>(payload.ToArray());
            
            //新しいプレイヤーの情報ならDictionaryに追加する
            if (!_responses.ContainsKey(data.PlayerId))
            {
                _responses.Add(data.PlayerId, new PlayerCoordinateToResponse());
            }
            //プレイヤーの座標を更新する
            var newPosition = new ServerVector3(data.X,0,data.Y);
            _entitiesDatastore.SetPosition(data.PlayerId,newPosition);

            
            //プレイヤーの座標から返すチャンクのブロックデータを取得をする
            var responseChunk = GetChunkBytes(data);
            
            //エンティティのデータを取得する
            responseChunk.Add(GetEntityBytes(data));
            

            return responseChunk;
        }


        private List<List<byte>> GetChunkBytes(PlayerCoordinateSendProtocolMessagePack data)
        {
            var responseChunk = new List<List<byte>>();
            var responseChunkCoordinates = _responses[data.PlayerId].GetResponseChunkCoordinates(new Coordinate((int)data.X,(int) data.Y));
            foreach (var chunkCoordinate in responseChunkCoordinates)
            {
                //チャンクのブロックデータを取得してバイト配列に変換する
                responseChunk.Add(ChunkBlockToPayload.Convert(chunkCoordinate,_worldBlockDatastore,_worldMapTile));
            }

            return responseChunk;
        }


        private List<byte> GetEntityBytes(PlayerCoordinateSendProtocolMessagePack data)
        {
            //TODO 今はベルトコンベアのアイテムをエンティティとして返しているだけ 今後は本当のentityも返す
            var coordinate = new Coordinate((int)data.X,(int)data.Y);
            var responseChunkCoordinates = PlayerCoordinateToResponse.GetChunkCoordinates(coordinate);
            var items = CollectBeltConveyorItems.CollectItem(responseChunkCoordinates,_worldBlockDatastore,_blockConfig,_entityFactory);

            var response = MessagePackSerializer.Serialize(new EntitiesResponseMessagePack(items)).ToList();

            return response;
        }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class PlayerCoordinateSendProtocolMessagePack : ProtocolMessagePackBase
    {
        public PlayerCoordinateSendProtocolMessagePack(int playerId, float x, float y)
        {
            Tag = PlayerCoordinateSendProtocol.Tag;
            PlayerId = playerId;
            X = x;
            Y = y;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlayerCoordinateSendProtocolMessagePack() { }

        public int PlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}