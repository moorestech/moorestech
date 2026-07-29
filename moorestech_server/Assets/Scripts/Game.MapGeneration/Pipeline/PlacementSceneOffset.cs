using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline
{
    // ノイズ座標(タイルローカル + G)で算出された配置物を -G してシーン座標へ戻す変換。
    // 移植元 TerrainApplier.Apply(placementOffset) と同じ役割で、木はタイルローカル生成のため対象外。
    // Shifts placements computed in noise space (tile-local + G) back to scene space by -G.
    // Mirrors the reference TerrainApplier.Apply(placementOffset); trees are excluded as they are tile-local.
    public static class PlacementSceneOffset
    {
        public static void ShiftEntries(List<PlacementEntry> entries, Vector2 spawnOffset)
        {
            if (entries == null) return;
            var shift = new Vector3(spawnOffset.x, 0f, spawnOffset.y);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.WorldPosition -= shift;
                entries[i] = entry;
            }
        }

        // 鉱脈 AABB は整数スナップ済みなので、float の -G を掛けてから丸め直しタイルローカル格子へ戻す。
        // Vein AABBs are already integer-snapped, so apply the float -G and re-round back onto the tile-local lattice.
        public static void ShiftVeins(List<PlacedVein> veins, Vector2 spawnOffset)
        {
            var shift = new Vector3(spawnOffset.x, 0f, spawnOffset.y);
            foreach (var vein in veins)
            {
                vein.Min = Vector3Int.RoundToInt((Vector3)vein.Min - shift);
                vein.Max = Vector3Int.RoundToInt((Vector3)vein.Max - shift);
            }
        }
    }
}
