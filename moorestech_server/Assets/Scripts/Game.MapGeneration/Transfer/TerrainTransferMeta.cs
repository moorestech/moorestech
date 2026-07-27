using Game.MapGeneration.Provisioning;

namespace Game.MapGeneration.Transfer
{
    // 地形バイナリをクライアントへ分割転送するためのメタ情報。チャンク本体の転送はこの値を前提に行う
    // Metadata describing the terrain binaries for chunked transfer to clients; chunk transfer builds on these values
    public class TerrainTransferMeta
    {
        // 論理ストリームを切り出す単位(非圧縮)。チャンク総数の算出と実際の切り出しはこの値を共有する
        // Slice unit of the logical stream (uncompressed); chunk count and actual slicing share this value
        public const int ChunkByteSize = 256 * 1024;

        public readonly string MapMode;
        public readonly string WorldId;
        public readonly int TerrainResolution;
        public readonly int TerrainTileCount;
        public readonly int TerrainChunkTotal;

        public TerrainTransferMeta(string mapMode, string worldId, int terrainResolution, int terrainTileCount, int terrainChunkTotal)
        {
            MapMode = mapMode;
            WorldId = worldId;
            TerrainResolution = terrainResolution;
            TerrainTileCount = terrainTileCount;
            TerrainChunkTotal = terrainChunkTotal;
        }

        // ワールドディレクトリを持たない構成(テスト・クライアント単体デバッグ)用。地形もワールド同一性も存在しない
        // For configurations without a world directory (tests, standalone client debug): neither terrain nor world identity exists
        public static TerrainTransferMeta CreateWithoutWorldDirectory()
        {
            return new TerrainTransferMeta(WorldProvisioner.TemplateMapMode, string.Empty, 0, 0, 0);
        }
    }
}
