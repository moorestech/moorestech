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
                    mapObjects.Add(new MapObjectLayoutMessagePack(
                        mapObject.InstanceId, mapObject.MapObjectGuidStr, mapObject.X, mapObject.Y, mapObject.Z,
                        mapObject.RotationX, mapObject.RotationY, mapObject.RotationZ, mapObject.RotationW,
                        mapObject.ScaleX, mapObject.ScaleY, mapObject.ScaleZ,
                        mapObject.ClusterId, mapObject.ClusterCenterX, mapObject.ClusterCenterZ));

                var mapVeins = new List<VeinLayoutMessagePack>();
                foreach (var vein in _mapInfoJson.MapVeins)
                    mapVeins.Add(new VeinLayoutMessagePack(vein.VeinGuidStr, vein.MinX, vein.MinY, vein.MinZ, vein.MaxX, vein.MaxY, vein.MaxZ));

                var terrainMeta = TerrainTransferMetaReader.Read(_worldDataDirectory);

                // 同じメタをハッシュ計算へ渡し、総チャンク数とハッシュの参照時点を揃える
                // Reuse the same metadata so chunk totals and the hash describe one snapshot
                return new ResponseMapDataMessagePack(spawn, mapObjects, mapVeins, terrainMeta, TerrainStreamHasher.Compute(_worldDataDirectory, terrainMeta));
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

            // 地形メタとハッシュの束。地形なしワールドはTerrainResolution=0・TerrainHash=""で表明される
            // Terrain meta and hash bundle; a terrain-less world shows TerrainResolution=0 and an empty TerrainHash
            [Key(5)] public TerrainTransferMetaMessagePack TerrainMeta { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseMapDataMessagePack() { }

            public ResponseMapDataMessagePack(Vector3MessagePack spawn, List<MapObjectLayoutMessagePack> mapObjects, List<VeinLayoutMessagePack> mapVeins,
                TerrainTransferMeta terrainMeta, string terrainHash)
            {
                Tag = ProtocolTag;
                Spawn = spawn;
                MapObjects = mapObjects;
                MapVeins = mapVeins;
                TerrainMeta = new TerrainTransferMetaMessagePack(terrainMeta, terrainHash);
            }
        }
    }
}
