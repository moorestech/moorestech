using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    /// <summary>
    ///     見た目キャッシュの往復と無効化を検証する。キーが合わない・中身が寸法と食い違う場合に
    ///     「取り逃し」へ倒れることが、壊れたキャッシュを黙って使わないことの担保になる
    ///     Verifies the visual cache's round trip and invalidation; falling back to a miss on a stale key or on content
    ///     disagreeing with its dimensions is what guarantees a broken cache is never used in silence
    /// </summary>
    public class TerrainVisualCacheTest
    {
        // 高さとalphamapは別解像度にする。同じ値だと区画の取り違えが往復で相殺されて見えなくなる
        // Heights and the alphamap take different resolutions; equal ones would let a section mix-up cancel out across the round trip
        private const int HeightmapResolution = 17;
        private const int AlphamapResolution = 4;
        private const int LayerCount = 3;
        private const int DetailResolution = 16;
        private const int DetailMapCount = 2;
        private const int TileX = 1;
        private const int TileZ = 2;

        // 実物と同じ64文字の16進。長さが違うと書き込み時点で弾かれる
        // The real 64-hex-character shape; a different length is rejected at write time
        private const string CacheKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string OtherCacheKey = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

        private string _worldRootDirectory;

        [SetUp]
        public void SetUp()
        {
            _worldRootDirectory = Path.Combine(Path.GetTempPath(), $"moorestech_visual_cache_test_{Guid.NewGuid()}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_worldRootDirectory)) Directory.Delete(_worldRootDirectory, true);
        }

        [Test]
        public void RestoresTheHeightsAlphamapAndDetailMapsItSaved()
        {
            var cache = CreateCache(CacheKey);
            cache.Save(TileX, TileZ, CreateTileVisual());

            Assert.That(TryLoad(cache, out var loaded), Is.True);

            var saved = CreateTileVisual();
            for (var z = 0; z < HeightmapResolution; z++)
            for (var x = 0; x < HeightmapResolution; x++)
                // 高さはushort量子化ぶんの誤差だけを許す。行や列が入れ替わればこの範囲には収まらない
                // Heights allow only the ushort quantization error; a swapped row or column never lands inside it
                Assert.That(loaded.DisplayHeights[z, x], Is.EqualTo(saved.DisplayHeights[z, x]).Within(1f / ushort.MaxValue),
                    $"height z={z} x={x}");

            Assert.That(loaded.Alphamap.Resolution, Is.EqualTo(AlphamapResolution));
            Assert.That(loaded.Alphamap.LayerCount, Is.EqualTo(LayerCount));
            Assert.That(loaded.Alphamap.Planes.Count, Is.EqualTo(saved.Alphamap.Planes.Count));
            for (var planeIndex = 0; planeIndex < saved.Alphamap.Planes.Count; planeIndex++)
                // 平面はそのままテクスチャへ載る。1バイトも動いてはいけない
                // A plane goes onto a texture verbatim, so not one byte may move
                Assert.That(loaded.Alphamap.Planes[planeIndex], Is.EqualTo(saved.Alphamap.Planes[planeIndex]), $"plane={planeIndex}");

            Assert.That(loaded.DetailMaps.Count, Is.EqualTo(saved.DetailMaps.Count));
            for (var mapIndex = 0; mapIndex < saved.DetailMaps.Count; mapIndex++)
            for (var z = 0; z < DetailResolution; z++)
            for (var x = 0; x < DetailResolution; x++)
                Assert.That(loaded.DetailMaps[mapIndex][z, x], Is.EqualTo(saved.DetailMaps[mapIndex][z, x]),
                    $"map={mapIndex} z={z} x={x}");
        }

        [Test]
        public void MissesWhenTheKeyDiffers()
        {
            CreateCache(CacheKey).Save(TileX, TileZ, CreateTileVisual());

            // マスタや地形が動けばキーが変わる。同じファイルでも別物として取り逃す
            // A moved master or terrain changes the key, so the same file is missed as a different thing
            Assert.That(TryLoad(CreateCache(OtherCacheKey), out _), Is.False);
        }

        [Test]
        public void MissesWhenTheTileDiffers()
        {
            CreateCache(CacheKey).Save(TileX, TileZ, CreateTileVisual());

            Assert.That(CreateCache(CacheKey).TryLoad(TileX, TileZ + 1, HeightmapResolution, AlphamapResolution, LayerCount,
                DetailResolution, DetailMapCount, out _), Is.False);
        }

        [Test]
        public void MissesWithAWarningWhenTheFileIsTruncated()
        {
            var cache = CreateCache(CacheKey);
            cache.Save(TileX, TileZ, CreateTileVisual());

            var filePath = WorldDataDirectory.FromWorldRoot(_worldRootDirectory).TerrainVisualCacheFilePath(TileX, TileZ);
            var bytes = File.ReadAllBytes(filePath);
            Array.Resize(ref bytes, bytes.Length - 1);
            File.WriteAllBytes(filePath, bytes);

            // 切り詰めは以降の全画素を1バイトずらす。黙って読むと草も地面も別物になる
            // A truncation shifts every later pixel by a byte; reading it silently would draw different ground and grass
            LogAssert.Expect(LogType.Warning, new Regex("Discarding"));
            Assert.That(TryLoad(cache, out _), Is.False);
        }

        [Test]
        public void MissesWithAWarningWhenTheHeaderIsIncomplete()
        {
            var cache = CreateCache(CacheKey);
            cache.Save(TileX, TileZ, CreateTileVisual());

            var filePath = WorldDataDirectory.FromWorldRoot(_worldRootDirectory).TerrainVisualCacheFilePath(TileX, TileZ);
            File.WriteAllBytes(filePath, new byte[1]);

            // ヘッダ途中のファイルも完成済みとして扱うと、次の起動で壊れた見た目を再利用してしまう
            // Treating a partial header as complete would reuse broken visuals on the next boot
            LogAssert.Expect(LogType.Warning, new Regex("Discarding"));
            Assert.That(TryLoad(cache, out _), Is.False);
        }

        [Test]
        public void MissesWithAWarningWhenTheHeightmapResolutionDisagrees()
        {
            var cache = CreateCache(CacheKey);
            cache.Save(TileX, TileZ, CreateTileVisual());

            // 高さの解像度が違えば区画の境界がずれ、alphamapとdetailを高さのバイト列から読み始める
            // A differing heightmap resolution shifts every section boundary and starts reading the alphamap and detail inside the height bytes
            LogAssert.Expect(LogType.Warning, new Regex("Discarding"));
            Assert.That(cache.TryLoad(TileX, TileZ, HeightmapResolution + 1, AlphamapResolution, LayerCount,
                DetailResolution, DetailMapCount, out _), Is.False);
        }

        [Test]
        public void MissesWithAWarningWhenTheAlphamapResolutionDisagrees()
        {
            var cache = CreateCache(CacheKey);
            cache.Save(TileX, TileZ, CreateTileVisual());

            LogAssert.Expect(LogType.Warning, new Regex("Discarding"));
            Assert.That(cache.TryLoad(TileX, TileZ, HeightmapResolution, AlphamapResolution + 1, LayerCount,
                DetailResolution, DetailMapCount, out _), Is.False);
        }

        [Test]
        public void MissesWithAWarningWhenTheLayerCountDisagrees()
        {
            var cache = CreateCache(CacheKey);
            cache.Save(TileX, TileZ, CreateTileVisual());

            // 層数がTerrainDataのterrainLayersと違うまま流すと、全画素が別のテクスチャで描かれる
            // Letting a layer count differ from TerrainData.terrainLayers would draw every pixel with a different texture
            LogAssert.Expect(LogType.Warning, new Regex("Discarding"));
            Assert.That(cache.TryLoad(TileX, TileZ, HeightmapResolution, AlphamapResolution, LayerCount + 1,
                DetailResolution, DetailMapCount, out _), Is.False);
        }

        [Test]
        public void MissesWithAWarningWhenTheDetailMapCountDisagrees()
        {
            var cache = CreateCache(CacheKey);
            cache.Save(TileX, TileZ, CreateTileVisual());

            // detail mapはprototypeと同じ順番で結び付く。数が違うキャッシュをhitにすると草種がずれる
            // Detail maps pair with prototypes in order; hitting a count-mismatched cache shifts vegetation types
            LogAssert.Expect(LogType.Warning, new Regex("Discarding"));
            Assert.That(cache.TryLoad(TileX, TileZ, HeightmapResolution, AlphamapResolution, LayerCount,
                DetailResolution, DetailMapCount + 1, out _), Is.False);
        }

        private static bool TryLoad(TerrainVisualCache cache, out TerrainTileVisual tileVisual)
        {
            return cache.TryLoad(TileX, TileZ, HeightmapResolution, AlphamapResolution, LayerCount, DetailResolution,
                DetailMapCount, out tileVisual);
        }

        private TerrainVisualCache CreateCache(string cacheKey)
        {
            return new TerrainVisualCache(WorldDataDirectory.FromWorldRoot(_worldRootDirectory), cacheKey);
        }

        // 画素ごとに違う値を敷く。並びが崩れれば必ずどこかの比較が落ちる
        // Every pixel gets a distinct value so any broken ordering fails some comparison
        private static TerrainTileVisual CreateTileVisual()
        {
            var displayHeights = new float[HeightmapResolution, HeightmapResolution];
            for (var z = 0; z < HeightmapResolution; z++)
            for (var x = 0; x < HeightmapResolution; x++)
                displayHeights[z, x] = (z * HeightmapResolution + x) /
                                       (float)(HeightmapResolution * HeightmapResolution - 1);

            var alphamap = new float[AlphamapResolution, AlphamapResolution, LayerCount];
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            for (var layer = 0; layer < LayerCount; layer++)
                alphamap[z, x, layer] = ((z * AlphamapResolution + x) * LayerCount + layer) / 63f;

            var detailMaps = new List<int[,]>();
            for (var mapIndex = 0; mapIndex < DetailMapCount; mapIndex++)
            {
                var detailMap = new int[DetailResolution, DetailResolution];
                for (var z = 0; z < DetailResolution; z++)
                for (var x = 0; x < DetailResolution; x++)
                    detailMap[z, x] = mapIndex * 100 + z * DetailResolution + x;

                detailMaps.Add(detailMap);
            }

            return new TerrainTileVisual(
                displayHeights,
                TileAlphamap.Create(StoredAlphamapWeights.ToPlanes(alphamap), AlphamapResolution, LayerCount),
                detailMaps);
        }
    }
}
