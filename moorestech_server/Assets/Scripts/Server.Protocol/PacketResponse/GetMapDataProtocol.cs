using System;
using System.Collections.Generic;
using Game.Map.Interface.Json;
using Game.MapGeneration.Transfer;
using Game.Paths;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     マップレイアウト（spawn/mapObjects/mapVeins）を返す読み取り専用プロトコル
    ///     Read-only protocol returning the map layout (spawn/mapObjects/mapVeins)
    /// </summary>
    public class GetMapDataProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:mapData";

        public enum MapDataMode
        {
            Layout,
            // TerrainChunkはP3で追加
            // TerrainChunk is added in P3
        }

        private readonly MapInfoJson _mapInfoJson;
        private readonly WorldDataDirectory _worldDataDirectory;

        public GetMapDataProtocol(ServiceProvider serviceProvider)
        {
            _mapInfoJson = serviceProvider.GetService<MapInfoJson>();
            _worldDataDirectory = serviceProvider.GetService<WorldDataDirectory>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<RequestMapDataMessagePack>(payload);

            // Modeごとに応答を切り替え、未知値はフォールバックせず例外にする
            // Dispatch by Mode; unknown values throw instead of falling back
            return request.Mode switch
            {
                MapDataMode.Layout => CreateLayoutResponse(),
                _ => throw new ArgumentException($"Unknown MapDataMode: {request.Mode}")
            };

            #region Internal

            ResponseMapDataMessagePack CreateLayoutResponse()
            {
                var spawnPoint = _mapInfoJson.DefaultSpawnPointJson;
                var spawn = new Vector3MessagePack(spawnPoint.X, spawnPoint.Y, spawnPoint.Z);

                var mapObjects = new List<MapObjectLayoutMessagePack>();
                foreach (var mapObject in _mapInfoJson.MapObjects)
                    mapObjects.Add(new MapObjectLayoutMessagePack(mapObject.InstanceId, mapObject.MapObjectGuidStr, mapObject.X, mapObject.Y, mapObject.Z));

                var mapVeins = new List<VeinLayoutMessagePack>();
                foreach (var vein in _mapInfoJson.MapVeins)
                    mapVeins.Add(new VeinLayoutMessagePack(vein.VeinGuidStr, vein.MinX, vein.MinY, vein.MinZ, vein.MaxX, vein.MaxY, vein.MaxZ));

                // 地形チャンクを要求するために必要なメタ情報をワールドディレクトリの実体から読む
                // Read the metadata clients need to request terrain chunks from the real world directory
                var terrainMeta = TerrainTransferMetaReader.Read(_worldDataDirectory);

                return new ResponseMapDataMessagePack(spawn, mapObjects, mapVeins, terrainMeta);
            }

            #endregion
        }



        [MessagePackObject]
        public class RequestMapDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public MapDataMode Mode { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestMapDataMessagePack() { }

            public RequestMapDataMessagePack(MapDataMode mode)
            {
                Tag = ProtocolTag;
                Mode = mode;
            }
        }

        [MessagePackObject]
        public class ResponseMapDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3MessagePack Spawn { get; set; }
            [Key(3)] public List<MapObjectLayoutMessagePack> MapObjects { get; set; }
            [Key(4)] public List<VeinLayoutMessagePack> MapVeins { get; set; }

            // 地形メタ。TerrainResolution=0はterrainを持たないワールド（template）を意味する
            // Terrain meta; TerrainResolution=0 means the world owns no terrain (template)
            [Key(5)] public string MapMode { get; set; }
            [Key(6)] public string WorldId { get; set; }
            [Key(7)] public int TerrainResolution { get; set; }
            [Key(8)] public int TerrainTileCount { get; set; }
            [Key(9)] public int TerrainChunkTotal { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseMapDataMessagePack() { }

            public ResponseMapDataMessagePack(Vector3MessagePack spawn, List<MapObjectLayoutMessagePack> mapObjects, List<VeinLayoutMessagePack> mapVeins, TerrainTransferMeta terrainMeta)
            {
                Tag = ProtocolTag;
                Spawn = spawn;
                MapObjects = mapObjects;
                MapVeins = mapVeins;
                MapMode = terrainMeta.MapMode;
                WorldId = terrainMeta.WorldId;
                TerrainResolution = terrainMeta.TerrainResolution;
                TerrainTileCount = terrainMeta.TerrainTileCount;
                TerrainChunkTotal = terrainMeta.TerrainChunkTotal;
            }
        }

        [MessagePackObject]
        public class MapObjectLayoutMessagePack
        {
            [Key(0)] public int InstanceId { get; set; }
            [Key(1)] public string MapObjectGuid { get; set; }
            [Key(2)] public float X { get; set; }
            [Key(3)] public float Y { get; set; }
            [Key(4)] public float Z { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MapObjectLayoutMessagePack() { }

            public MapObjectLayoutMessagePack(int instanceId, string mapObjectGuid, float x, float y, float z)
            {
                InstanceId = instanceId;
                MapObjectGuid = mapObjectGuid;
                X = x;
                Y = y;
                Z = z;
            }
        }

        [MessagePackObject]
        public class VeinLayoutMessagePack
        {
            [Key(0)] public string VeinGuid { get; set; }
            [Key(1)] public int MinX { get; set; }
            [Key(2)] public int MinY { get; set; }
            [Key(3)] public int MinZ { get; set; }
            [Key(4)] public int MaxX { get; set; }
            [Key(5)] public int MaxY { get; set; }
            [Key(6)] public int MaxZ { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public VeinLayoutMessagePack() { }

            public VeinLayoutMessagePack(string veinGuid, int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
            {
                VeinGuid = veinGuid;
                MinX = minX;
                MinY = minY;
                MinZ = minZ;
                MaxX = maxX;
                MaxY = maxY;
                MaxZ = maxZ;
            }
        }
    }
}
