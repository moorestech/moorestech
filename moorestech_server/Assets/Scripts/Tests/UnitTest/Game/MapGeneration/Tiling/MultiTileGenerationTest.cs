using System;
using System.Collections.Generic;
using System.Linq;
using Game.MapGeneration.Pipeline;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // gridSizeX/Z の格子ぶんタイルを生成し、原点・タイルindex・配置物のシーン座標が揃うことを検証する。
    // Verifies the generator emits one tile per gridSizeX/Z cell with consistent origins, indices, and scene-space placements.
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardServerMap1")]
    public class MultiTileGenerationTest
    {
        private const int GridSide = 3;
        private const int Seed = 7;

        // 探索無効時にだけ効く master 側の窓原点。0 だと基準の二重定義を区別できない。
        // The master-side window origin that matters only when the search is disabled; zero cannot tell the two bases apart.
        private const float MasterWorldOffset = 500f;

        // 境界にAABB候補を溢れさせる密度設定。3x3と組み合わせないので実行時間は増えない。
        // The density that floods the seam with AABB candidates; it never pairs with 3x3, so runtime does not grow.
        private const float SeamDenseDensity = 5f;
        private const int SeamDenseGridSide = 2;
        private const int SeamDenseSeed = 1;

        [Test]
        public void グリッド設定どおりのタイル数と原点が出力される()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);

            var output = new VanillaGenerator().Generate(config).Output;

            Assert.AreEqual(GridSide * GridSide, output.Tiles.Count);

            // SceneOrigin = (-half*W, -half*L)。3x3 なら half=1 で中心タイルがシーン (0,W)x(0,L) を占める
            // SceneOrigin = (-half*W, -half*L): with 3x3, half=1, so the center tile occupies scene (0,W)x(0,L)
            Assert.AreEqual(new Vector2(-config.terrainWidth, -config.terrainLength), output.SceneOrigin);

            // 不変条件 NoiseOrigin - SceneOrigin = G。G は探索後の config.worldOffset で、探索無効なら master 値のまま
            // Invariant NoiseOrigin - SceneOrigin = G; G is the post-search config.worldOffset, staying at the master value when the search is disabled
            Assert.AreEqual(new Vector2(config.worldOffsetX, config.worldOffsetZ), output.NoiseOrigin - output.SceneOrigin);

            var indices = output.Tiles.Select(tile => new Vector2Int(tile.TileX, tile.TileZ)).ToList();
            Assert.AreEqual(GridSide * GridSide, indices.Distinct().Count(), "タイルindexが重複している");
            Assert.IsTrue(indices.Contains(new Vector2Int(1, 1)), "中心タイルが無い");

            foreach (var tile in output.Tiles)
                Assert.AreEqual(config.Resolution * config.Resolution, tile.Heights.Length);
        }

        // 転送層のEnumerateTileCoordinatesは正方格子前提。非正方はindexとcoordの対応が崩れるので生成側で先に弾く
        // The transfer layer's EnumerateTileCoordinates assumes a square grid; the generator rejects a non-square one before it can break the index-to-coord mapping
        [Test]
        public void 正方形でないグリッド設定は例外で拒否される()
        {
            var config = MultiTileTestWorld.BuildConfig(2, Seed);
            config.gridSizeZ = 3;
            Assert.Throws<InvalidOperationException>(() => new VanillaGenerator().Generate(config));
        }

        // 0と負値は正方判定(gridSizeX == gridSizeZ)を素通りするため、正方チェックとは別条件で弾く必要がある
        // Zero and negative sides slip past the square check (gridSizeX == gridSizeZ), so they need their own guard
        [TestCase(0)]
        [TestCase(-1)]
        public void グリッドサイズが0以下なら例外で拒否される(int gridSide)
        {
            var config = MultiTileTestWorld.BuildConfig(gridSide, Seed);
            Assert.Throws<InvalidOperationException>(() => new VanillaGenerator().Generate(config));
        }

        [Test]
        public void 単一タイル設定では現行と同じ原点になる()
        {
            var config = MultiTileTestWorld.BuildConfig(1, Seed);

            var output = new VanillaGenerator().Generate(config).Output;

            Assert.AreEqual(1, output.Tiles.Count);
            Assert.AreEqual(Vector2.zero, output.SceneOrigin);
        }

        // タイルごとに worldOffset をずらしていなければ全タイルが同じ地形になる。
        // Without shifting worldOffset per tile every tile would carry the very same terrain.
        [Test]
        public void 隣接タイルは別のノイズ窓を見て異なる高さになる()
        {
            var output = new VanillaGenerator().Generate(MultiTileTestWorld.BuildConfig(GridSide, Seed)).Output;

            var center = output.Tiles.Single(tile => tile.TileX == 1 && tile.TileZ == 1);
            var right = output.Tiles.Single(tile => tile.TileX == 2 && tile.TileZ == 1);

            Assert.IsFalse(center.Heights.SequenceEqual(right.Heights));
        }

        // MapObjects は木(タイルローカル)と岩(ノイズ座標)の両方を含み、どちらかの変換が漏れると中心タイルへ折り重なる。
        // MapObjects hold both trees (tile-local) and rocks (noise-space); a missing shift on either piles them onto the center tile.
        [Test]
        public void 配置物は全タイルのシーン座標へ広がる()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);

            var output = new VanillaGenerator().Generate(config).Output;

            Assert.IsNotEmpty(output.MapObjects);
            var buckets = new HashSet<Vector2Int>();
            foreach (var mapObject in output.MapObjects)
            {
                MultiTileTestWorld.AssertInsideGrid(mapObject.Position.x, mapObject.Position.z, config);
                buckets.Add(MultiTileTestWorld.TileBucket(mapObject.Position.x, mapObject.Position.z, config));
            }

            Assert.Less(1, buckets.Count, "配置物が単一タイルへ固まっている");
            Assert.IsTrue(buckets.Any(bucket => bucket != Vector2Int.zero), "中心タイル以外に配置物が無い");
        }

        // 鉱脈は output のリストへ加算されるべきで、タイルごとに代入すると最後のタイルぶんしか残らない。
        // Veins must accumulate into the output lists; assigning per tile would keep only the last tile's veins.
        [Test]
        public void 鉱脈は全タイルぶん蓄積される()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);

            var output = new VanillaGenerator().Generate(config).Output;

            Assert.IsNotEmpty(output.ItemVeins);
            var buckets = new HashSet<Vector2Int>();
            foreach (var vein in output.ItemVeins)
            {
                MultiTileTestWorld.AssertVeinInsideGrid(vein, config);
                buckets.Add(MultiTileTestWorld.TileBucket(vein.Min.x, vein.Min.z, config));
            }

            Assert.Less(1, buckets.Count, "鉱脈が単一タイルぶんしか残っていない");
        }

        // 非重なりを支えるのはタイル境界の帯なので多タイルでも通す
        // The seam band upholds non-overlap, so this runs on a multi-tile world
        [Test]
        public void 多タイルでも鉱脈AABBは重ならない()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);

            var output = new VanillaGenerator().Generate(config).Output;

            Assert.IsNotEmpty(output.ItemVeins);
            MultiTileTestWorld.AssertNoOverlappingVeins(
                output.ItemVeins.Concat(output.FluidVeins).ToList());
        }

        // シーン座標化の基準が探索の戻り値(探索無効なら0)だと、地形の窓原点だけが master worldOffset ぶん進む。
        // A basis taken from the search result (zero when disabled) advances only the terrain window origin by the master worldOffset.
        [Test]
        public void 探索無効かつmaster_worldOffsetありでも地形と配置物とスポーンが同じフレームに乗る()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);
            config.worldOffsetX = MasterWorldOffset;
            config.worldOffsetZ = MasterWorldOffset;

            // spawnWorldPosition はノイズ座標なので窓原点 + 中心タイルの1/4点に置く（シーンでは1/4点そのもの）
            // spawnWorldPosition is noise-space, so place it at the window origin plus the center tile's quarter point
            config.spawnWorldPosition = new Vector2(
                MasterWorldOffset + config.terrainWidth * 0.25f,
                MasterWorldOffset + config.terrainLength * 0.25f);

            var output = new VanillaGenerator().Generate(config).Output;

            Assert.AreEqual(new Vector2(MasterWorldOffset, MasterWorldOffset), output.NoiseOrigin - output.SceneOrigin);

            // 基準が0だとスポーンは -G されず窓原点ぶん残るため、地形とは別フレームの座標になる
            // With a zero basis the spawn is never shifted by -G and keeps the window origin, landing in a different frame than the terrain
            Assert.AreEqual(config.terrainWidth * 0.25f, output.SpawnPoint.x, 0.001f, "スポーンXが地形と別フレーム");
            Assert.AreEqual(config.terrainLength * 0.25f, output.SpawnPoint.z, 0.001f, "スポーンZが地形と別フレーム");

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                MultiTileTestWorld.AssertInsideGrid(mapObject.Position.x, mapObject.Position.z, config);

            Assert.IsNotEmpty(output.ItemVeins);
            foreach (var vein in output.ItemVeins)
            {
                MultiTileTestWorld.AssertVeinInsideGrid(vein, config);
            }
        }

        // 種類別haloだけでは防げない境界AABB候補を密にして、統一台帳の非重なりだけを見る。
        // Densifies the seam AABB candidates that per-kind halos cannot reject and checks only the unified ledger's non-overlap.
        [Test]
        public void 密な境界AABB候補でも多タイルの鉱脈AABBは重ならない()
        {
            var config = MultiTileTestWorld.BuildConfig(SeamDenseGridSide, SeamDenseSeed);
            config.oreConfig.borderMargin = 0f;
            foreach (var entry in config.oreConfig.entries.Concat(config.oreConfig.fluidEntries))
            {
                entry.bands[0].density = SeamDenseDensity;
                entry.bands[0].maxObjectsPerCluster = 1;
                entry.bands[0].clusterRadius = 0f;
                entry.bands[0].minDistanceBetweenOres = 0f;
            }

            var output = new VanillaGenerator().Generate(config).Output;

            Assert.IsNotEmpty(output.ItemVeins);
            MultiTileTestWorld.AssertNoOverlappingVeins(
                output.ItemVeins.Concat(output.FluidVeins).ToList());
        }
    }
}
