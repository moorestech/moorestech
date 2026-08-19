using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // 配置系がワールド座標基準で抽選され、タイル跨ぎの最小距離も守ることを固定する。
    // Pins that placement draws in world space and honours the minimum distance across tile seams.
    public class TilePlacementWorldSpaceTest
    {
        private const int GridSide = 3;
        private const int Seed = 7;

        // タイルローカル座標を突き合わせる粒度(1mm)。反復していれば座標はビット単位で一致するのでこの粒度でも捕まり、
        // 独立に抽選された木同士が 1mm まで一致する偶然は起こらない。
        // The granularity for matching tile-local coordinates (1mm): a repeat matches bit for bit and is still caught here,
        // while two independently drawn trees never agree to the millimetre by chance.
        private const float LocalCoordinateKeyScale = 1000f;

        // TreePrototypeEntry.sharedGridMinDistance と UnderstoryConfig.understoryNeighborRadius の既定値。
        // The defaults of TreePrototypeEntry.sharedGridMinDistance and UnderstoryConfig.understoryNeighborRadius.
        private const float TreeMinDistance = 2f;

        // タイル毎に同じ乱数列と同じノイズ場を回すと、格子全体が同じ絵を反復する。
        // Running the same random stream and noise field per tile makes the whole grid repeat one picture.
        [Test]
        public void 木のタイルローカル座標が別タイルへ反復しない()
        {
            var config = BuildTreeOnlyConfig();
            var output = new VanillaGenerator().Generate(config);
            Assert.IsNotEmpty(output.MapObjects);

            var tilesPerLocalCoordinate = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
            foreach (var mapObject in output.MapObjects)
            {
                var tile = MultiTileTestWorld.TileBucket(mapObject.Position.x, mapObject.Position.z, config);
                var key = new Vector2Int(
                    Mathf.RoundToInt((mapObject.Position.x - tile.x * config.terrainWidth) * LocalCoordinateKeyScale),
                    Mathf.RoundToInt((mapObject.Position.z - tile.y * config.terrainLength) * LocalCoordinateKeyScale));
                if (!tilesPerLocalCoordinate.TryGetValue(key, out var tiles))
                {
                    tiles = new HashSet<Vector2Int>();
                    tilesPerLocalCoordinate.Add(key, tiles);
                }
                tiles.Add(tile);
            }

            var repeated = new List<string>();
            foreach (var pair in tilesPerLocalCoordinate)
            {
                if (pair.Value.Count <= 1) continue;
                repeated.Add($"local(mm)={pair.Key} tiles={string.Join("/", pair.Value)}");
            }

            Assert.AreEqual(0, repeated.Count,
                $"同一タイルローカル座標が複数タイルに現れている: {repeated.Count}件 {string.Join(" ", repeated.GetRange(0, Mathf.Min(3, repeated.Count)))}");
        }

        // 近傍グリッドがタイル内で閉じていると、境界の帯だけ最小距離が破られる。
        // A neighbour grid closed inside one tile lets the seam band break the minimum distance.
        [Test]
        public void タイル境界を跨ぐ木も最小距離を守る()
        {
            var config = BuildTreeOnlyConfig();
            var output = new VanillaGenerator().Generate(config);
            Assert.IsNotEmpty(output.MapObjects);

            // 全ペア総当たりは木の本数に対して重すぎるので、最小距離を辺長とするセルで近傍だけ突き合わせる。
            // All-pairs is far too heavy for this many trees, so bucket into cells of the minimum distance and compare neighbours only.
            var cells = new Dictionary<Vector2Int, List<TreePoint>>();
            foreach (var mapObject in output.MapObjects)
            {
                var point = new TreePoint
                {
                    Position = new Vector2(mapObject.Position.x, mapObject.Position.z),
                    Tile = MultiTileTestWorld.TileBucket(mapObject.Position.x, mapObject.Position.z, config),
                };
                var cell = CellOf(point.Position);
                if (!cells.TryGetValue(cell, out var bucket))
                {
                    bucket = new List<TreePoint>();
                    cells.Add(cell, bucket);
                }
                bucket.Add(point);
            }

            var closest = float.MaxValue;
            foreach (var pair in cells)
                foreach (var point in pair.Value)
                    closest = Mathf.Min(closest, NearestFromOtherTile(cells, point));

            Assert.LessOrEqual(TreeMinDistance - 0.001f, closest,
                $"タイルを跨ぐ木の最小距離が {closest}m で、設定値 {TreeMinDistance}m を下回っている");
        }

        // 木だけを出す設定。岩を止めるのは MapObjects の中身を木に限定して距離判定の対象を揃えるため。
        // A tree-only setup; rocks are off so MapObjects holds trees alone and the distance test compares like with like.
        private static TerrainGenerationConfig BuildTreeOnlyConfig()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);
            config.generateObject = false;
            return config;
        }

        private static Vector2Int CellOf(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / TreeMinDistance), Mathf.FloorToInt(position.y / TreeMinDistance));
        }

        // 自セル±1セルの中から、別タイル出自の点までの最短距離を返す。
        // Returns the shortest distance to a point originating from another tile, searching the own cell plus one ring.
        private static float NearestFromOtherTile(Dictionary<Vector2Int, List<TreePoint>> cells, TreePoint origin)
        {
            var center = CellOf(origin.Position);
            var nearest = float.MaxValue;
            for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!cells.TryGetValue(new Vector2Int(center.x + dx, center.y + dy), out var bucket)) continue;
                    foreach (var candidate in bucket)
                    {
                        if (candidate.Tile == origin.Tile) continue;
                        nearest = Mathf.Min(nearest, Vector2.Distance(candidate.Position, origin.Position));
                    }
                }
            return nearest;
        }

        private struct TreePoint
        {
            public Vector2 Position;
            public Vector2Int Tile;
        }
    }
}
