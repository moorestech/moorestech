using System;
using System.Collections.Generic;
using System.Linq;
using Game.Base;
using Game.Block.Interface.BlockConfig;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.MessagePack;
using Server.Protocol.PacketResponse.Player;
using Server.Protocol.PacketResponse.Util;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     
    /// </summary>
    public class PlayerCoordinateSendProtocol : IPacketResponse
    {
        public const string Tag = "va:playerCoordinate";
        public const string ChunkDataTag = "va:chunkData";
        public const string EntityDataTag = "va:entityData";
        private readonly IBlockConfig _blockConfig;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IEntityFactory _entityFactory;

        private readonly Dictionary<int, PlayerCoordinateToResponse> _responses = new();

        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly WorldMapTile _worldMapTile;

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

            //Dictionary
            if (!_responses.ContainsKey(data.PlayerId)) _responses.Add(data.PlayerId, new PlayerCoordinateToResponse());
            
            var newPosition = new ServerVector3(data.X, 0, data.Y);
            _entitiesDatastore.SetPosition(data.PlayerId, newPosition);


            
            var response = GetChunkBytes(data);

            
            var entityResponse = GetEntityBytes(data);
            if (entityResponse != null) response.Add(entityResponse);


            return response;
        }


        private List<List<byte>> GetChunkBytes(PlayerCoordinateSendProtocolMessagePack data)
        {
            var responseChunk = new List<List<byte>>();
            var responseChunkCoordinates = _responses[data.PlayerId].GetResponseChunkCoordinates(new Coordinate((int)data.X, (int)data.Y));
            foreach (var chunkCoordinate in responseChunkCoordinates)
                
                responseChunk.Add(ChunkBlockToPayload.Convert(chunkCoordinate, _worldBlockDatastore, _worldMapTile));

            return responseChunk;
        }


        private List<byte> GetEntityBytes(PlayerCoordinateSendProtocolMessagePack data)
        {
            //TODO  entity
            var coordinate = new Coordinate((int)data.X, (int)data.Y);
            var responseChunkCoordinates = PlayerCoordinateToResponse.GetChunkCoordinates(coordinate);
            var items = CollectBeltConveyorItems.CollectItem(responseChunkCoordinates, _worldBlockDatastore, _blockConfig, _entityFactory);


            if (items.Count == 0) return null;

            var response = MessagePackSerializer.Serialize(new EntitiesResponseMessagePack(items)).ToList();

            return response;
        }
    }


    [MessagePackObject(true)]
    public class PlayerCoordinateSendProtocolMessagePack : ProtocolMessagePackBase
    {
        public PlayerCoordinateSendProtocolMessagePack(int playerId, float x, float y)
        {
            Tag = PlayerCoordinateSendProtocol.Tag;
            PlayerId = playerId;
            X = x;
            Y = y;
        }

        [Obsolete("。。")]
        public PlayerCoordinateSendProtocolMessagePack()
        {
        }

        public int PlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}