using System;
using System.Collections.Generic;
using Game.Map.Interface.Json;
using Game.MapGeneration.Transfer;
using Game.Paths;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     マップレイアウト（spawn/mapObjects/mapVeins）と地形バイナリのチャンクを返す読み取り専用プロトコル
    ///     Read-only protocol returning the map layout (spawn/mapObjects/mapVeins) and terrain binary chunks
    /// </summary>
    public class GetMapDataProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:mapData";

        public enum MapDataMode
        {
            Layout,
            TerrainChunk,
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
                MapDataMode.TerrainChunk => CreateTerrainChunkResponse(),
                _ => throw new ArgumentException($"Unknown MapDataMode: {request.Mode}")
            };

            #region Internal

            ProtocolMessagePackBase CreateLayoutResponse()
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

                return new ResponseMapDataMessagePack(spawn, mapObjects, mapVeins, terrainMeta, TerrainChunkReader.ComputeStreamHash(_worldDataDirectory));
            }

            ProtocolMessagePackBase CreateTerrainChunkResponse()
            {
                // 地形なしワールドや範囲外indexはTerrainChunkReaderが例外にする。ここで握り潰さない
                // A terrain-less world or an out-of-range index throws inside TerrainChunkReader; never swallow it here
                return new ResponseMapDataTerrainChunkMessagePack(request.ChunkIndex, TerrainChunkReader.Read(_worldDataDirectory, request.ChunkIndex));
            }

            #endregion
        }

        [MessagePackObject]
        public class RequestMapDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public MapDataMode Mode { get; set; }

            // TerrainChunk時のみ意味を持つ。Modeごとの必要項目はstatic factoryで固定する
            // Meaningful only for TerrainChunk; the static factories pin down what each mode needs
            [Key(3)] public int ChunkIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestMapDataMessagePack() { }

            private RequestMapDataMessagePack(MapDataMode mode, int chunkIndex)
            {
                Tag = ProtocolTag;
                Mode = mode;
                ChunkIndex = chunkIndex;
            }

            public static RequestMapDataMessagePack CreateLayoutRequest()
            {
                return new RequestMapDataMessagePack(MapDataMode.Layout, 0);
            }

            public static RequestMapDataMessagePack CreateTerrainChunkRequest(int chunkIndex)
            {
                return new RequestMapDataMessagePack(MapDataMode.TerrainChunk, chunkIndex);
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

            // 論理ストリーム全体のSHA256。クライアントのキャッシュ鮮度判定用で、地形なしワールドは空文字
            // SHA256 of the whole logical stream for client cache validation; empty for terrain-less worlds
            [Key(10)] public string TerrainHash { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseMapDataMessagePack() { }

            public ResponseMapDataMessagePack(Vector3MessagePack spawn, List<MapObjectLayoutMessagePack> mapObjects, List<VeinLayoutMessagePack> mapVeins,
                TerrainTransferMeta terrainMeta, string terrainHash)
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
                TerrainHash = terrainHash;
            }
        }
    }
}
