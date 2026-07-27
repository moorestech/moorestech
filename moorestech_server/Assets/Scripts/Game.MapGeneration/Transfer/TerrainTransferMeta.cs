using System;
using System.Collections.Generic;
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

        // 論理ストリームを構成するタイルの並び順。一辺√TileCountの正方格子をz行→x列で走査する
        // Tile order composing the logical stream: a square grid of side sqrt(TileCount), scanned row (z) then column (x)
        public static List<(int TileX, int TileZ)> EnumerateTileCoordinates(int terrainTileCount)
        {
            // 正方格子でないタイル数は並び順が定義できない。推測で補正せず例外にする
            // A non-square tile count has no defined ordering; throw instead of guessing a correction
            var tilesPerSide = (int)Math.Round(Math.Sqrt(terrainTileCount));
            if (tilesPerSide * tilesPerSide != terrainTileCount)
                throw new InvalidOperationException($"Terrain tile count must be a perfect square, but was {terrainTileCount}.");

            var tileCoordinates = new List<(int TileX, int TileZ)>(terrainTileCount);
            for (var tileZ = 0; tileZ < tilesPerSide; tileZ++)
            for (var tileX = 0; tileX < tilesPerSide; tileX++)
                tileCoordinates.Add((tileX, tileZ));
            return tileCoordinates;
        }

        // ワールドディレクトリを持たない構成(テスト・クライアント単体デバッグ)用。地形もワールド同一性も存在しない
        // For configurations without a world directory (tests, standalone client debug): neither terrain nor world identity exists
        public static TerrainTransferMeta CreateWithoutWorldDirectory()
        {
            return new TerrainTransferMeta(WorldProvisioner.TemplateMapMode, string.Empty, 0, 0, 0);
        }
    }
}
