using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using GenVanilla = Mooresmaster.Model.GenerationModule.VanillaGeneratorAlgorithmParam;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // 生成型 oreConfig → 実行時 WorldOreConfig POCO。item(entries)/fluid(fluidEntries) は同形で、
    // prefab は持たず veinGuid（mapVeins）を文字列で保持する。biomes は BiomeFlags へ合成する。
    // Converts generated oreConfig to the runtime WorldOreConfig POCO; item(entries)/fluid(fluidEntries)
    // share a shape, carry veinGuid strings instead of prefabs, and compose biomes into BiomeFlags.
    internal static class OreRuntimeConfigFactory
    {
        public static WorldOreConfig Build(GenVanilla vp)
        {
            var ore = vp.OreConfig;
            var result = new WorldOreConfig { borderMargin = ore.BorderMargin };

            var items = new List<OreEntry>();
            if (ore.Entries != null)
            foreach (var e in ore.Entries)
                items.Add(new OreEntry
                {
                    veinGuid = e.VeinGuid.ToString(),
                    biomes = RuntimeConvert.ToBiomeFlags(e.Biomes),
                    bands = ToBands(e.Bands, b => b.OuterRadiusMeters, b => b.Density,
                        b => b.MaxObjectsPerCluster, b => b.ClusterRadius,
                        b => b.MinDistanceBetweenOres, b => b.PlacementRetries),
                    useSlopeFilter = e.UseSlopeFilter,
                    slopeMax = e.SlopeMax,
                    slopeSmoothness = e.SlopeSmoothness,
                    minDistanceFromOthers = e.MinDistanceFromOthers
                });
            result.entries = items.ToArray();

            var fluids = new List<OreEntry>();
            if (ore.FluidEntries != null)
            foreach (var e in ore.FluidEntries)
                fluids.Add(new OreEntry
                {
                    veinGuid = e.VeinGuid.ToString(),
                    biomes = RuntimeConvert.ToBiomeFlags(e.Biomes),
                    bands = ToBands(e.Bands, b => b.OuterRadiusMeters, b => b.Density,
                        b => b.MaxObjectsPerCluster, b => b.ClusterRadius,
                        b => b.MinDistanceBetweenOres, b => b.PlacementRetries),
                    useSlopeFilter = e.UseSlopeFilter,
                    slopeMax = e.SlopeMax,
                    slopeSmoothness = e.SlopeSmoothness,
                    minDistanceFromOthers = e.MinDistanceFromOthers
                });
            result.fluidEntries = fluids.ToArray();

            return result;
        }

        // item/fluid で要素型が異なるため Func セレクタで共通化する。
        // Item/fluid have different element types, so band conversion is shared via Func selectors.
        static OreBand[] ToBands<T>(T[] bands,
            Func<T, float> outer, Func<T, float> density, Func<T, int> maxObjects,
            Func<T, float> clusterRadius, Func<T, float> minBetween, Func<T, int> retries)
        {
            if (bands == null) return new OreBand[0];
            var result = new OreBand[bands.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                var b = bands[i];
                result[i] = new OreBand
                {
                    outerRadiusMeters = outer(b),
                    density = density(b),
                    maxObjectsPerCluster = maxObjects(b),
                    clusterRadius = clusterRadius(b),
                    minDistanceBetweenOres = minBetween(b),
                    placementRetries = retries(b)
                };
            }
            return result;
        }
    }
}
