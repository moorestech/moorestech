using System;
using System.Collections.Generic;
using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Transfer
{
    // 地形分割転送用メタ
    // Terrain chunk-transfer metadata
    public class TerrainTransferMeta
    {
        // 論理ストリームを切り出す単位(非圧縮)。チャンク総数の算出と実際の切り出しはこの値を共有する
        // Slice unit of the logical stream (uncompressed); chunk count and actual slicing share this value
        public const int ChunkByteSize = 256 * 1024;

        public readonly string MapMode;

        // モード文字列の解釈はこの型の中だけで終わらせる。消費側は文字列比較を持たない
        // Mode-string interpretation ends inside this type; consumers never compare the string themselves
        public readonly bool IsTemplate;

        public readonly string WorldId;
        public readonly int TerrainResolution;
        public readonly int TerrainTileCount;
        public readonly int TerrainChunkTotal;

        // world.jsonのseedそのもの。クライアントは転送地形と整合する分類段(海陸・ビーチ・バイオーム重み)をこのseedで再現する
        // The world.json seed verbatim; clients reproduce the classification stage (land/sea, beach, biome weights) consistent with the transferred terrain from it
        public readonly int WorldSeed;

        // 生成時のノイズ窓原点とシーン原点。seedと同じく生成時にしか決まらずマスタからは復元できない値
        // The generation-time noise window origin and scene origin; like the seed, they exist only at generation and cannot be recovered from the master
        public readonly TerrainOrigins Origins;

        // 生成マスタの指紋(JSON原文+配置ノイズPNG)。templateは空文字。ワールド作成時のマスタとの一致検査に使う
        // The generation master's fingerprint (JSON text + placement-noise PNGs); empty for template. Used to check agreement with the master at world creation
        public readonly string GenerationMasterFingerprint;

        // 転送ファイル構成の版。ワールドを作ったビルドの値そのもので、templateは空文字
        // The transferred file layout's version, verbatim from the build that created the world; empty for template
        public readonly string GeneratorVersion;

        private TerrainTransferMeta(string mapMode, string worldId, int terrainResolution, int terrainTileCount, int terrainChunkTotal, int worldSeed,
            TerrainOrigins origins, string generationMasterFingerprint, string generatorVersion)
        {
            MapMode = mapMode;
            IsTemplate = mapMode == WorldMapMode.Template;
            WorldId = worldId;
            TerrainResolution = terrainResolution;
            TerrainTileCount = terrainTileCount;
            TerrainChunkTotal = terrainChunkTotal;
            WorldSeed = worldSeed;
            Origins = origins;
            GenerationMasterFingerprint = generationMasterFingerprint;
            GeneratorVersion = generatorVersion;
        }

        public static TerrainTransferMeta CreateGenerated(
            string worldId, int terrainResolution, int terrainTileCount, int terrainChunkTotal, int worldSeed, TerrainOrigins origins,
            string generationMasterFingerprint, string generatorVersion)
        {
            return new TerrainTransferMeta(
                WorldMapMode.Generated, worldId, terrainResolution, terrainTileCount, terrainChunkTotal, worldSeed, origins,
                generationMasterFingerprint, generatorVersion);
        }

        public static TerrainTransferMeta CreateTemplate(string worldId, int worldSeed)
        {
            return new TerrainTransferMeta(
                WorldMapMode.Template, worldId, 0, 0, 0, worldSeed, TerrainOrigins.WithoutTerrain(), string.Empty, string.Empty);
        }

        // ワイヤ値をドメインへ戻す唯一の入口。モード文字列の解釈と未知モードの拒否はプロトコルDTOではなくこの型が持つ
        // The single entry restoring wire values into the domain; interpreting the mode string and rejecting unknown ones belongs here, not to the protocol DTO
        public static TerrainTransferMeta FromWire(
            string mapMode, string worldId, int terrainResolution, int terrainTileCount, int terrainChunkTotal, int worldSeed,
            TerrainOrigins origins, string generationMasterFingerprint, string generatorVersion)
        {
            if (mapMode == WorldMapMode.Template) return CreateTemplate(worldId, worldSeed);
            if (mapMode == WorldMapMode.Generated)
                return CreateGenerated(worldId, terrainResolution, terrainTileCount, terrainChunkTotal, worldSeed, origins,
                    generationMasterFingerprint, generatorVersion);
            throw new InvalidOperationException($"[TerrainTransferMeta] Unknown map mode '{mapMode}'.");
        }

        // generatedなのにチャンク0本は生成失敗かファイル切り詰め。地形なしと同一視すると壊れたワールドを正常として配る
        // Zero chunks in generated mode means a failed generation or truncated files; equating it with terrain-less would ship a broken world as healthy
        public void ThrowIfGeneratedWorldOwnsNoChunk()
        {
            if (0 < TerrainChunkTotal) return;
            throw new InvalidOperationException(
                $"Generated world '{WorldId}' owns zero terrain chunk: the terrain files are missing or truncated.");
        }

        // 現在のビルドとの照合(生成マスタ指紋・生成器の版)はTerrainTransferMetaCompatibilityが持つ。この型自身の値だけで完結する検査はここに置く
        // Matching against the current build (master fingerprint, generator version) lives in TerrainTransferMetaCompatibility; checks closed over this type's own values stay here

        // 解像度照合は唯一の実装へ集約する
        // The resolution check is consolidated into a single implementation
        public void ThrowIfTerrainResolutionDiffers(int currentResolution)
        {
            if (currentResolution == TerrainResolution) return;
            throw new InvalidOperationException(
                $"Generation master resolution {currentResolution} disagrees with the transferred terrain resolution {TerrainResolution}.");
        }

        // 原点照合も同じく唯一の実装へ集約する。原点がずれた台帳は別の窓の配置なので、無言で見た目へ流さない
        // The origin check is consolidated the same way; a ledger built on shifted origins holds another window's placements and must never reach the visuals silently
        public void ThrowIfOriginsDiffer(Vector2 currentNoiseOrigin, Vector2 currentSceneOrigin)
        {
            // 原点はfloatでworld.jsonを往復し、注入時のG=NoiseOrigin-SceneOriginでも丸められる（数km地点のfloat刻みは約1mm）
            // 実際にずれるときは窓1枚ぶん（数百m以上）動くので、ハイトマップ1サンプル(約4m)の400分の1を同一とみなす閾値にする
            // The origins round-trip through world.json as floats and are rounded again by the injected G = NoiseOrigin - SceneOrigin (a float step is about 1mm several km out)
            // A real disagreement moves by a whole window (hundreds of metres), so the threshold sits at a four-hundredth of one heightmap sample (about 4m)
            const float toleranceMeters = 0.01f;
            if (Vector2.Distance(currentNoiseOrigin, Origins.NoiseOrigin) <= toleranceMeters &&
                Vector2.Distance(currentSceneOrigin, Origins.SceneOrigin) <= toleranceMeters) return;

            throw new InvalidOperationException(
                $"Regenerated origins (noise {currentNoiseOrigin}, scene {currentSceneOrigin}) disagree with the transferred origins " +
                $"(noise {Origins.NoiseOrigin}, scene {Origins.SceneOrigin}).");
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

        // ワールドディレクトリを持たない構成(テスト・クライアント単体デバッグ)用。地形もワールド同一性も存在しない
        // world.jsonが無いのでseedという概念自体が存在せず0を置く。WorldIdを空文字にしているのと同じ「不在」の表明
        // For configurations without a world directory (tests, standalone client debug): neither terrain nor world identity exists
        // With no world.json there is no seed concept at all, so 0 declares absence just as the empty WorldId does
        public static TerrainTransferMeta CreateWithoutWorldDirectory()
        {
            return CreateTemplate(string.Empty, 0);
        }
    }
}
