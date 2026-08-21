using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置物をシーン座標へ揃える変換をまとめる。ノイズ座標のものは窓原点ぶん引き、タイルローカルの木はタイル位置ぶん進める。
    // 移植元 TerrainApplier.Apply(placementOffset) と、チャンクGameObjectをcoord*幅へ置く処理に対応する。
    // Collects the shifts that bring placements into scene space: subtract the window origin for noise-space ones, add the tile position for tile-local trees.
    // Mirrors the reference TerrainApplier.Apply(placementOffset) plus placing the chunk GameObject at coord*size.
    public static class PlacementSceneOffset
    {
        // 木はタイルローカル(0..terrainWidth)で生成されるため、タイルの設置位置ぶん平行移動する。
        // Trees are generated tile-local (0..terrainWidth), so they translate by the tile's placement position.
        public static void ToTileScene(List<PlacementEntry> entries, Vector2 tileScene)
        {
            var shift = new Vector3(tileScene.x, 0f, tileScene.y);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.WorldPosition += shift;
                entries[i] = entry;
            }
        }

        public static void ToSceneSpace(List<PlacementEntry> entries, Vector2 noiseToSceneShift)
        {
            var shift = new Vector3(noiseToSceneShift.x, 0f, noiseToSceneShift.y);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.WorldPosition -= shift;

                // クラスタ重心もノイズ座標のままなので、配置位置と同じシフトを通してフレームを揃える。
                // The cluster centroid is also still in noise space, so apply the same shift to keep it in the same frame as the position.
                // 独立配置(ClusterId=-1)の重心は未設定の (0,0,0) なので、引くと未使用値が -shift の実座標へ化ける。
                // An independent placement (ClusterId=-1) leaves the centroid at an unset (0,0,0), which subtraction would turn into a real -shift coordinate.
                if (entry.Cluster.HasValue && 0 <= entry.Cluster.Value.ClusterId)
                {
                    var cluster = entry.Cluster.Value;
                    cluster.Center -= shift;
                    entry.Cluster = cluster;
                }

                entries[i] = entry;
            }
        }

        // 鉱脈 AABB は整数スナップ済みなので、窓原点シフトを整数へ1度だけ丸めて全件へ同じ値を引く。
        // Vein AABBs are already integer-snapped, so the window-origin shift rounds to an integer once and every vein subtracts that same value.
        //
        // vein ごとに丸め直すと隣り合う AABB の間隔が 1 縮み、ノイズ空間で確立した非重なりが壊れる。
        // Re-rounding per vein can shrink the gap between neighbouring AABBs by one and break the non-overlap established in noise space.
        public static void ToSceneSpace(List<PlacedVein> veins, Vector2 noiseToSceneShift)
        {
            var offset = Vector3Int.RoundToInt(new Vector3(noiseToSceneShift.x, 0f, noiseToSceneShift.y));
            foreach (var vein in veins)
            {
                vein.Min -= offset;
                vein.Max -= offset;
            }
        }
    }
}
