using System;
using System.Collections.Generic;
using Game.Paths;

namespace Game.MapGeneration.Transfer
{
    // 地形分割転送用メタの基底。template/generatedの違いは判別子ではなく派生型そのものが表す
    // Base of the terrain chunk-transfer metadata; the template/generated split is the derived type itself, not a discriminator
    public abstract class TerrainTransferMeta
    {
        // 論理ストリームを切り出す単位(非圧縮)。チャンク総数の算出と実際の切り出しはこの値を共有する
        // Slice unit of the logical stream (uncompressed); chunk count and actual slicing share this value
        public const int ChunkByteSize = 256 * 1024;

        // ワイヤへ載せるモード文字列。値は派生型が決め、外から与えられることはない
        // The mode string put on the wire; the derived type settles it and nothing outside supplies it
        public readonly string MapMode;

        public readonly string WorldId;

        // world.jsonのseedそのもの。クライアントは転送地形と整合する分類段(海陸・ビーチ・バイオーム重み)をこのseedで再現する
        // The world.json seed verbatim; clients reproduce the classification stage (land/sea, beach, biome weights) consistent with the transferred terrain from it
        public readonly int WorldSeed;

        protected TerrainTransferMeta(string mapMode, string worldId, int worldSeed)
        {
            MapMode = mapMode;
            WorldId = worldId;
            WorldSeed = worldSeed;
        }

        // ワイヤ値をドメインへ戻す唯一の入口。モード文字列の解釈と未知モードの拒否はプロトコルDTOではなくこの型が持つ
        // The single entry restoring wire values into the domain; interpreting the mode string and rejecting unknown ones belongs here, not to the protocol DTO
        public static TerrainTransferMeta FromWire(
            string mapMode, string worldId, int terrainResolution, int terrainTileCount, int terrainChunkTotal, int worldSeed,
            TerrainOrigins origins, string generationMasterFingerprint, string generatorVersion, string placementLedgerDigest)
        {
            if (mapMode == WorldMapMode.Template) return new TemplateTerrainTransferMeta(worldId, worldSeed);
            if (mapMode == WorldMapMode.Generated)
            {
                WorldGeneratorVersion.ThrowIfDiffers(generatorVersion, worldId);
                return new GeneratedTerrainTransferMeta(
                    worldId, terrainResolution, terrainTileCount, terrainChunkTotal, worldSeed,
                    new GeneratedTerrainTransferPayload(origins, generationMasterFingerprint, generatorVersion, placementLedgerDigest));
            }

            throw new InvalidOperationException($"[TerrainTransferMeta] Unknown map mode '{mapMode}'.");
        }

        // ワールドディレクトリを持たない構成(テスト・クライアント単体デバッグ)用。地形もワールド同一性も存在しない
        // world.jsonが無いのでseedという概念自体が存在せず0を置く。WorldIdを空文字にしているのと同じ「不在」の表明
        // For configurations without a world directory (tests, standalone client debug): neither terrain nor world identity exists
        // With no world.json there is no seed concept at all, so 0 declares absence just as the empty WorldId does
        public static TemplateTerrainTransferMeta CreateWithoutWorldDirectory()
        {
            return new TemplateTerrainTransferMeta(string.Empty, 0);
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

        // 論理ストリームを構成するファイルの並び。タイル順にheightだけを並べる
        // File order composing the logical stream: per tile, height only
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

        // タイル1枚が論理ストリームへ寄与するファイル。1画素あたりのバイト数の唯一の定義
        // The files one tile contributes: the single definition of the bytes-per-pixel
        private static IEnumerable<(string FilePath, int BytesPerPixel)> EnumerateTileFiles(WorldDataDirectory worldDataDirectory, int tileX, int tileZ)
        {
            yield return (worldDataDirectory.TerrainHeightFilePath(tileX, tileZ), 2);
        }
    }
}
