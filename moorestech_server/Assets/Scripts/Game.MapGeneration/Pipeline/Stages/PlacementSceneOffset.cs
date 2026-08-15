using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置物をシーン座標へ揃える変換をまとめる。ノイズ座標のものは -G、タイルローカルの木はタイル位置ぶん進める。
    // 移植元 TerrainApplier.Apply(placementOffset) と、チャンクGameObjectをcoord*幅へ置く処理に対応する。
    // Collects the shifts that bring placements into scene space: -G for noise-space ones, +tile position for tile-local trees.
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
                entries[i] = entry;
            }
        }

        // 鉱脈 AABB は整数スナップ済みなので、float の -G を掛けてから丸め直しタイルローカル格子へ戻す。
        // Vein AABBs are already integer-snapped, so apply the float -G and re-round back onto the tile-local lattice.
        public static void ToSceneSpace(List<PlacedVein> veins, Vector2 noiseToSceneShift)
        {
            var shift = new Vector3(noiseToSceneShift.x, 0f, noiseToSceneShift.y);
            foreach (var vein in veins)
            {
                vein.Min = Vector3Int.RoundToInt((Vector3)vein.Min - shift);
                vein.Max = Vector3Int.RoundToInt((Vector3)vein.Max - shift);
            }
        }
    }
}
