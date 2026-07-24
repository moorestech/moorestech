using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // Stage 6: 鉱脈のクラスター配置。各エントリの対象バイオーム合成マスク内で PoissonDisk 中心→
    // 極座標クラスター展開の順に処理する。prefab はスキーマ化で veinGuid（mapVeins）へ置換した。
    // Stage 6: vein cluster placement. Within each entry's composite biome mask, processes
    // PoissonDisk centers then polar cluster expansion. prefab replaced by veinGuid (mapVeins).
    public static class OrePlacementGenerator
    {
        // ワールド全体の鉱脈を配置する。entryMasks[i] は entries[i] の対象バイオーム合成マスク。
        // Places all veins; entryMasks[i] is the composite biome mask for entries[i].
        public static List<PlacementEntry> GenerateForWorld(
            OreEntry[] entries,
            bool[][,] entryMasks,
            float borderMargin,
            float[,] heights,
            TerrainDimensions dims,
            System.Random rng,
            SpatialGrid treeSpatialGrid,
            SpatialGrid objectSpatialGrid)
        {
            var result = new List<PlacementEntry>();
            if (entries == null || entries.Length == 0)
                return result;

            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float borderPx = BiomeMaskBuilder.MetersToPixels(borderMargin, w, hRes);

            // 鉱石メンバー・クラスター中心の距離チェック用グリッド（全エントリ共有）。
            // Shared grids for ore-member and cluster-center distance checks (across entries).
            var oreGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));
            var clusterCenterGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));

            // クラスター中心の共有間隔（全エントリ・全バンド横断の clusterRadius*2.5 の最大値）。
            // Shared cluster-center spacing (max of clusterRadius*2.5 across all entries/bands).
            float centerSpacing = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.veinGuid) || entry.bands == null) continue;
                if (entryMasks == null || i >= entryMasks.Length || entryMasks[i] == null) continue;
                foreach (var b in entry.bands)
                    if (b != null) centerSpacing = Mathf.Max(centerSpacing, b.clusterRadius * 2.5f);
            }

            // クラスター識別子はこの生成呼び出し内で 0 から採番する決定論ローカルカウンタ。
            // 同一 seed 再現のためグローバル NextClusterId には依存しない（AABB グルーピングの鍵）。
            // Cluster ids come from a deterministic local counter starting at 0 per Generate call,
            // never the global NextClusterId, so same-seed output stays identical (AABB grouping key).
            int nextClusterId = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.veinGuid)) continue;
                if (entryMasks == null || i >= entryMasks.Length || entryMasks[i] == null) continue;
                OreEntryPlacer.Place(entry, entryMasks[i], heights, dims, rng,
                    borderPx, treeSpatialGrid, objectSpatialGrid,
                    oreGrid, clusterCenterGrid, centerSpacing, result, ref nextClusterId);
            }

            return result;
        }
    }
}
