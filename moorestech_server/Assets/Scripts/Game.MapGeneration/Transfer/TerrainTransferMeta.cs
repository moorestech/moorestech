using System;
using System.Collections.Generic;
using Game.MapGeneration.Provisioning;
using Game.Paths;

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
            // 0以下は完全平方判定を素通りしてチャンク0本のワイヤ値になる。地形を持つ前提の呼び出しなので例外にする
            // Non-positive counts slip past the square check and yield a zero-chunk wire value; callers assume terrain exists
            if (terrainTileCount <= 0)
                throw new InvalidOperationException($"Terrain tile count must be positive, but was {terrainTileCount}.");

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

        // 論理ストリームを構成するファイルの並び。タイル順にheight→biomeを交互に並べる
        // File order composing the logical stream: per tile, height then biome, interleaved
        public static IEnumerable<string> EnumerateStreamFilePaths(WorldDataDirectory worldDataDirectory, int terrainTileCount)
        {
            foreach (var tile in EnumerateTileCoordinates(terrainTileCount))
            foreach (var tileFile in EnumerateTileFiles(worldDataDirectory, tile.TileX, tile.TileZ))
                yield return tileFile.FilePath;
        }

        // 並び順に各ファイルの想定バイト長を添えた列挙。ファイル境界を実体に依らず決める用途(受信側の復元)に使う
        // Same order, annotated with each file's expected byte length, for deciding boundaries without the files (client restore)
        public static IEnumerable<(string FilePath, long ByteLength)> EnumerateStreamSegments(
            WorldDataDirectory worldDataDirectory, int terrainTileCount, int terrainResolution)
        {
            foreach (var tile in EnumerateTileCoordinates(terrainTileCount))
            foreach (var tileFile in EnumerateTileFiles(worldDataDirectory, tile.TileX, tile.TileZ))
                yield return (tileFile.FilePath, (long)terrainResolution * terrainResolution * tileFile.BytesPerPixel);
        }

        // タイル1枚が論理ストリームへ寄与するファイル。並び(height→biome)と1画素あたりのバイト数の唯一の定義
        // The files one tile contributes: the single definition of both the order (height then biome) and bytes per pixel
        private static IEnumerable<(string FilePath, int BytesPerPixel)> EnumerateTileFiles(WorldDataDirectory worldDataDirectory, int tileX, int tileZ)
        {
            yield return (worldDataDirectory.TerrainHeightFilePath(tileX, tileZ), 2);
            yield return (worldDataDirectory.TerrainBiomeFilePath(tileX, tileZ), 1);
        }

        // ワールドディレクトリを持たない構成(テスト・クライアント単体デバッグ)用。地形もワールド同一性も存在しない
        // For configurations without a world directory (tests, standalone client debug): neither terrain nor world identity exists
        public static TerrainTransferMeta CreateWithoutWorldDirectory()
        {
            return new TerrainTransferMeta(WorldProvisioner.TemplateMapMode, string.Empty, 0, 0, 0);
        }
    }
}
