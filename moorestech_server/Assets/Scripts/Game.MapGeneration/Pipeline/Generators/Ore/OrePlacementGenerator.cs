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
            PlacementHaloChannelMap centerHalos,
            float haloRadius)
        {
            var result = new List<PlacementEntry>();
            if (entries == null || entries.Length == 0)
                return result;

            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float borderPx = BiomeMaskBuilder.MetersToPixels(borderMargin, w, hRes);

            // 鉱石メンバーの距離チェック用グリッド（全エントリ共有・minDistanceBetweenOres/minDistanceFromOthersが使う）。
            // Shared grid for ore-member distance checks (used by minDistanceBetweenOres / minDistanceFromOthers across entries).
            var oreGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));

            // 確定済みの隣タイルの鉱脈を先に入れる。木と同じく、入れないと境界の帯だけ最小距離が破られる。
            // The already-confirmed neighbouring veins go in first; as with trees, the seam band would otherwise break the minimum distance.
            memberHalo.SeedGrid(oreGrid, dims.WorldOffsetX, dims.WorldOffsetZ, w, l, haloRadius);

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.veinGuid)) continue;
                if (entryMasks == null || i >= entryMasks.Length || entryMasks[i] == null) continue;

                // 中心排他はエントリ内に閉じる。全鉱脈共有だと先行エントリの中心が後続の候補を面で締め出す。
                // Center exclusion stays within the entry; sharing across veins lets earlier entries blanket later candidates out.
                float centerSpacing = 0f;
                if (entry.bands != null)
                    foreach (var band in entry.bands)
                        if (band != null) centerSpacing = Mathf.Max(centerSpacing, band.clusterRadius * 2.5f);

                var clusterCenterGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));
                var centerHalo = centerHalos.Get(entry.veinGuid);
                centerHalo.SeedGrid(clusterCenterGrid, dims.WorldOffsetX, dims.WorldOffsetZ, w, l, haloRadius);

                OreEntryPlacer.Place(entry, entryMasks[i], heights, dims, rng,
                    borderPx, treeSpatialGrid, objectSpatialGrid,
                    oreGrid, clusterCenterGrid, centerSpacing, centerHalo, result);
            }

            // このタイルで確定したメンバーを次のタイルの halo へ渡す。座標は既にワールドなので補正は要らない。
            // Hands this tile's confirmed members to the next tile's halo; the coordinates are already world-space.
            memberHalo.AddPlacements(result, 0f, 0f);

            return result;
        }
    }
}
