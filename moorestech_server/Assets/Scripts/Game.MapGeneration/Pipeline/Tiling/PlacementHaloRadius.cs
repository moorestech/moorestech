using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // 近傍判定が隣タイルを見に行く距離を、マスタが並べた全制約の最大値として1つ決める。
    // 制約ごとに刻むと拾い漏れが「境界の帯だけ最小距離が破れる」形でしか現れないため、格子で1つに揃える。
    // Settles one distance at which the neighbour tests reach into the adjacent tile: the maximum of every constraint the master states.
    // Splitting it per constraint would surface a miss only as a seam band breaking the minimum distance, so the whole grid shares one value.
    public static class PlacementHaloRadius
    {
        public static float Resolve(TerrainGenerationConfig config, BiomeType[] biomeTypes, BiomePlacementHelper helper)
        {
            var radius = 0f;
            foreach (var biome in biomeTypes)
            {
                radius = Mathf.Max(radius, TreeReach(helper.GetTreePlacementConfig(biome)));
                radius = Mathf.Max(radius, ObjectReach(helper.GetObjectConfig(biome)));
            }

            radius = Mathf.Max(radius, VeinReach(config.oreConfig.entries));
            radius = Mathf.Max(radius, VeinReach(config.oreConfig.fluidEntries));
            return radius;
        }

        static float TreeReach(TreePlacementConfig treeConfig)
        {
            var radius = 0f;
            if (treeConfig?.prototypes == null) return radius;
            foreach (var prototype in treeConfig.prototypes)
            {
                if (prototype == null) continue;
                radius = Mathf.Max(radius, prototype.sharedGridMinDistance);
                if (prototype.densityConfig != null)
                    radius = Mathf.Max(radius, prototype.densityConfig.localDensityCapRadius);
                if (prototype.understoryConfig != null)
                    radius = Mathf.Max(radius, prototype.understoryConfig.understoryNeighborRadius);
            }
            return radius;
        }

        static float ObjectReach(BiomeObjectConfig objectConfig)
        {
            var radius = 0f;
            if (objectConfig == null) return radius;
            if (objectConfig.entries != null)
                foreach (var entry in objectConfig.entries)
                {
                    if (entry == null) continue;
                    radius = Mathf.Max(radius, entry.minDistanceFromTree);

                    // maxDistanceFromTree は「近くに木があること」を求める側なので、隣タイルの木を見落とすと逆に配置が消える。
                    // maxDistanceFromTree demands a tree nearby, so missing the neighbouring tile's trees deletes placements instead.
                    radius = Mathf.Max(radius, entry.maxDistanceFromTree);
                }
            if (objectConfig.clusterEntries != null)
                foreach (var cluster in objectConfig.clusterEntries)
                {
                    if (cluster == null) continue;
                    radius = Mathf.Max(radius, cluster.minDistanceFromTree);
                    if (cluster.secondaries == null) continue;
                    foreach (var secondary in cluster.secondaries)
                        if (secondary != null) radius = Mathf.Max(radius, secondary.minDistanceFromTree);
                }
            return radius;
        }

        // クラスター中心の間隔は OrePlacementGenerator と同じ clusterRadius*2.5 で、鉱脈側の最大到達になる。
        // The cluster-center spacing is OrePlacementGenerator's clusterRadius*2.5, the widest reach on the vein side.
        static float VeinReach(OreEntry[] entries)
        {
            var radius = 0f;
            if (entries == null) return radius;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                radius = Mathf.Max(radius, entry.minDistanceFromOthers);
                if (entry.bands == null) continue;
                foreach (var band in entry.bands)
                {
                    if (band == null) continue;
                    radius = Mathf.Max(radius, band.minDistanceBetweenOres);
                    radius = Mathf.Max(radius, band.clusterRadius * 2.5f);
                }
            }
            return radius;
        }
    }
}
