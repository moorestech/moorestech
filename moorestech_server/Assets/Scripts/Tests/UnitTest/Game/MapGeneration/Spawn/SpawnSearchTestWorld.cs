using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Stages;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // スポーン探索系テストが共有する 5x5 格子ワールドの組み立てと、その格子に対する出力の範囲判定をまとめる。
    // Builds the 5x5 grid world shared by the spawn-search tests and checks outputs against that grid's extent.
    public static class SpawnSearchTestWorld
    {
        // 探索範囲 DefaultScanExtent は gridSize×terrainWidth なので、探索経路は本番と同じ 5x5 で検証する（裁定A4）。
        // DefaultScanExtent scales with gridSize x terrainWidth, so the search path is verified on the production 5x5 (ruling A4).
        public const int GridSide = 5;

        public static Generation CreateGeneration(TestGenerationConfigFactory.SpawnSearchSetup setup)
        {
            return CreateGeneration(setup, new JObject());
        }

        // 格子サイズは未指定のときだけ 5x5 を補う。factory の既定は1タイルで、そのままだと探索範囲が5分の1に縮む。
        // 呼び出し側が明示指定した値（例: 意図的な4x4境界テスト）は上書きしない
        // Fill in 5x5 only when unset: the factory defaults to a single tile, which would shrink the scan extent fivefold.
        // A value the caller already stated (e.g. an intentional 4x4 boundary test) is left untouched
        public static Generation CreateGeneration(
            TestGenerationConfigFactory.SpawnSearchSetup setup, JObject algorithmParamOverrides)
        {
            if (!algorithmParamOverrides.ContainsKey("gridSizeX")) algorithmParamOverrides["gridSizeX"] = GridSide;
            if (!algorithmParamOverrides.ContainsKey("gridSizeZ")) algorithmParamOverrides["gridSizeZ"] = GridSide;
            return TestGenerationConfigFactory.CreateWithAlgorithmParamOverrides(setup, algorithmParamOverrides);
        }

        // SceneOrigin = (-half×W, -half×L)。index(0,0) タイルの左下隅で、格子全体のシーン範囲の起点でもある
        // SceneOrigin = (-half x W, -half x L): the index(0,0) tile's lower-left corner and the start of the grid's scene extent
        public static Vector2 ExpectedSceneOrigin(Generation generation)
        {
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            return new Vector2(-(GridSide / 2) * vp.TerrainWidth, -(GridSide / 2) * vp.TerrainLength);
        }

        public static MapGenerationOutput AssertOutputIsInsideGrid(Generation generation, int seed)
        {
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            var output = MapGenerationPipeline.Generate(generation, seed, TestGenerationConfigFactory.ServerDataDirectory);

            // 格子が占める範囲を決めるのは SceneOrigin と格子サイズ。master の worldOffset を原点に使うと実位置とずれる。
            // SceneOrigin plus the grid size decides the extent; using the master worldOffset as origin would diverge from where the tiles really are.
            var minX = output.SceneOrigin.x;
            var minZ = output.SceneOrigin.y;
            var maxX = minX + GridSide * vp.TerrainWidth;
            var maxZ = minZ + GridSide * vp.TerrainLength;

            // スポーン地点は S-G = spawnTarget に構造的に一致する。テスト mod は overrideSpawnScenePosition=false かつ
            // gridSizeX/Z が奇数なので spawnTarget は GridCenterWorld = 格子中心になる。
            // The spawn is structurally S-G = spawnTarget. With overrideSpawnScenePosition=false and odd gridSizeX/Z
            // in the test mod, spawnTarget is GridCenterWorld, the grid's center.
            Assert.That(output.SpawnPoint.x, Is.EqualTo((minX + maxX) * 0.5f).Within(0.01f));
            Assert.That(output.SpawnPoint.z, Is.EqualTo((minZ + maxZ) * 0.5f).Within(0.01f));

            AssertMapObjectsSpreadInsideGrid(output, vp, minX, maxX, minZ, maxZ);
            AssertVeinsInsideGrid(output.ItemVeins, minX, maxX, minZ, maxZ);
            AssertVeinsInsideGrid(output.FluidVeins, minX, maxX, minZ, maxZ);
            return output;
        }

        private static void AssertMapObjectsSpreadInsideGrid(
            MapGenerationOutput output, VanillaGeneratorAlgorithmParam vp,
            float minX, float maxX, float minZ, float maxZ)
        {
            Assert.That(output.MapObjects, Is.Not.Empty);

            // 範囲判定は格子全域へ広がったぶん緩いので、タイルを跨いで散っていることも併せて見る
            // The range check loosened when it widened to the whole grid, so also require the placements to span several tiles
            var occupiedTiles = new HashSet<Vector2Int>();
            foreach (var mapObject in output.MapObjects)
            {
                Assert.That(mapObject.Position.x, Is.InRange(minX, maxX));
                Assert.That(mapObject.Position.z, Is.InRange(minZ, maxZ));
                occupiedTiles.Add(new Vector2Int(
                    Mathf.FloorToInt(mapObject.Position.x / vp.TerrainWidth),
                    Mathf.FloorToInt(mapObject.Position.z / vp.TerrainLength)));

                // 初期カメラとプレイヤーを塞がないよう、全mapObjectの中心をスポーンから15m以上離す
                // Keep every map-object center at least 15m from spawn so it cannot block the player or initial camera
                var distance = new Vector2(
                    mapObject.Position.x - output.SpawnPoint.x, mapObject.Position.z - output.SpawnPoint.z);
                Assert.That(distance.sqrMagnitude, Is.GreaterThanOrEqualTo(15f * 15f));
            }
            // 25タイル格子に対し1枚超では「中心タイルへ全部固まる」退行しか捕まえられない。3枚以上を要求し
            // 単一・隣接2タイルへの偏り退行も検知できるようにする
            // >1 out of a 25-tile grid only catches a "everything piles onto the center tile" regression;
            // requiring at least 3 also catches a bias toward a single tile or an adjacent pair of tiles
            Assert.That(occupiedTiles.Count, Is.GreaterThanOrEqualTo(3), "配置物が少数タイルへ偏っている");
        }

        private static void AssertVeinsInsideGrid(
            List<PlacedVein> veins, float minX, float maxX, float minZ, float maxZ)
        {
            Assert.That(veins, Is.Not.Empty);

            // 鉱脈は軸ごとに VeinAabbBuilder.Extent ぶん張り出すため、判定に軸別の余白を持たせる。
            // Veins reach VeinAabbBuilder.Extent per axis, so the range check takes an axis-specific margin.
            var marginX = VeinAabbBuilder.Extent.x;
            var marginZ = VeinAabbBuilder.Extent.z;
            foreach (var vein in veins)
            {
                Assert.That(vein.Min.x, Is.InRange(minX - marginX, maxX + marginX));
                Assert.That(vein.Max.x, Is.InRange(minX - marginX, maxX + marginX));
                Assert.That(vein.Min.z, Is.InRange(minZ - marginZ, maxZ + marginZ));
                Assert.That(vein.Max.z, Is.InRange(minZ - marginZ, maxZ + marginZ));
            }
        }
    }
}
