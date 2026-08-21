using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Tiling;
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
            SpatialGrid objectSpatialGrid,
            PlacementHaloChannel memberHalo,
            PlacementHaloChannel centerHalo,
            float haloRadius)
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

            // 確定済みの隣タイルの鉱脈を先に入れる。木と同じく、入れないと境界の帯だけ最小距離が破られる。
            // The already-confirmed neighbouring veins go in first; as with trees, the seam band would otherwise break the minimum distance.
            memberHalo.SeedGrid(oreGrid, dims.WorldOffsetX, dims.WorldOffsetZ, w, l, haloRadius);
            centerHalo.SeedGrid(clusterCenterGrid, dims.WorldOffsetX, dims.WorldOffsetZ, w, l, haloRadius);

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
                    oreGrid, clusterCenterGrid, centerSpacing, centerHalo, result, ref nextClusterId);
            }

            // このタイルで確定したメンバーを次のタイルの halo へ渡す。座標は既にワールドなので補正は要らない。
            // Hands this tile's confirmed members to the next tile's halo; the coordinates are already world-space.
            memberHalo.AddPlacements(result, 0f, 0f);

            return result;
        }
    }
}
