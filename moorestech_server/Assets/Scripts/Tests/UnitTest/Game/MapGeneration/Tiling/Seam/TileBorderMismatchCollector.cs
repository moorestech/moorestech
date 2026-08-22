using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling.Seam
{
    // 隣接タイル境界の height/biome 不一致を集める共有ヘルパー。呼び出し側は境界位置を1組ずつ渡すだけでよい。
    // Shared helper collecting height/biome mismatches at adjacent tile borders; the caller only feeds one border pair at a time.
    public static class TileBorderMismatchCollector
    {
        // 左タイルの最右列と右タイルの最左列は同一ワールドXをサンプルする
        // The left tile's rightmost column and the right tile's leftmost one sample the same world X
        public static void CollectColumnBorderMismatches(
            TerrainTileOutput left, TerrainTileOutput right, byte[] leftBiomeIndices, byte[] rightBiomeIndices,
            int resolution, float heightTolerance, List<string> heightMismatches, List<string> biomeMismatches)
        {
            for (var z = 0; z < resolution; z++)
                CollectSampleMismatch(left, right, leftBiomeIndices, rightBiomeIndices,
                    z * resolution + (resolution - 1), z * resolution, $"z={z}", heightTolerance, heightMismatches, biomeMismatches);
        }

        // 手前タイルの最奥行と奥タイルの最手前行は同一ワールドZをサンプルする
        // The near tile's farthest row and the far tile's nearest one sample the same world Z
        public static void CollectRowBorderMismatches(
            TerrainTileOutput near, TerrainTileOutput far, byte[] nearBiomeIndices, byte[] farBiomeIndices,
            int resolution, float heightTolerance, List<string> heightMismatches, List<string> biomeMismatches)
        {
            for (var x = 0; x < resolution; x++)
                CollectSampleMismatch(near, far, nearBiomeIndices, farBiomeIndices,
                    (resolution - 1) * resolution + x, x, $"x={x}", heightTolerance, heightMismatches, biomeMismatches);
        }

        private static void CollectSampleMismatch(
            TerrainTileOutput near, TerrainTileOutput far, byte[] nearBiomeIndices, byte[] farBiomeIndices,
            int nearIndex, int farIndex, string borderPosition, float heightTolerance,
            List<string> heightMismatches, List<string> biomeMismatches)
        {
            var location = $"({near.TileX},{near.TileZ})->({far.TileX},{far.TileZ}) {borderPosition}";
            if (heightTolerance < Mathf.Abs(near.Heights[nearIndex] - far.Heights[farIndex]))
                heightMismatches.Add($"{location}: {near.Heights[nearIndex]} vs {far.Heights[farIndex]}");
            if (nearBiomeIndices[nearIndex] != farBiomeIndices[farIndex])
                biomeMismatches.Add($"{location}: {nearBiomeIndices[nearIndex]} vs {farBiomeIndices[farIndex]}");
        }
    }
}
