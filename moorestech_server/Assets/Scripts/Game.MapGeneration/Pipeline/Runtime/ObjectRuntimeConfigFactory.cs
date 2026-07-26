using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using GenObject = Mooresmaster.Model.BiomeObjectConfigModule.BiomeObjectConfig;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // 生成型 biomeObjectConfig → 実行時 BiomeObjectConfig POCO。clusterEntries/secondaries/entries の
    // prefab 参照は mapObjectGuid 文字列配列へ写し、入れ子は var で辿る。
    // Converts generated biomeObjectConfig to the runtime BiomeObjectConfig POCO; prefab references
    // in clusterEntries/secondaries/entries become mapObjectGuid strings, nesting via var.
    internal static class ObjectRuntimeConfigFactory
    {
        public static BiomeObjectConfig Build(GenObject gen)
        {
            var result = new BiomeObjectConfig { borderMargin = gen.BorderMargin };

            var clusters = new List<ObjectClusterEntry>();
            if (gen.ClusterEntries != null)
            foreach (var ce in gen.ClusterEntries)
            {
                var cluster = new ObjectClusterEntry
                {
                    primary = RuntimeConvert.ToGuidStrings(ce.Primary, x => x.MapObjectGuid),
                    density = ce.Density,
                    scaleRange = ce.ScaleRange,
                    slopeAlignment = ce.SlopeAlignment,
                    sinkRange = ce.SinkRange,
                    noiseType = RuntimeConvert.ToMapNoiseType(ce.NoiseType),
                    noiseFrequency = ce.NoiseFrequency,
                    noiseAmplitude = ce.NoiseAmplitude,
                    noiseThreshold = ce.NoiseThreshold,
                    clusterCount = ce.ClusterCount,
                    objectsPerCluster = ce.ObjectsPerCluster,
                    clusterRadius = ce.ClusterRadius,
                    minDistanceFromTree = ce.MinDistanceFromTree
                };

                var secondaries = new List<ObjectClusterSecondary>();
                if (ce.Secondaries != null)
                foreach (var s in ce.Secondaries)
                {
                    secondaries.Add(new ObjectClusterSecondary
                    {
                        mode = RuntimeConvert.ToSecondaryMode(s.Mode),
                        mapObjectGuids = RuntimeConvert.ToGuidStrings(s.Prefabs, x => x.MapObjectGuid),
                        scaleRange = s.ScaleRange,
                        slopeAlignment = s.SlopeAlignment,
                        sinkRange = s.SinkRange,
                        countPerCluster = s.CountPerCluster,
                        minDistanceFromTree = s.MinDistanceFromTree,
                        minDistance = s.MinDistance,
                        maxDistance = s.MaxDistance,
                        density = s.Density,
                        clusterRadius = s.ClusterRadius
                    });
                }
                cluster.secondaries = secondaries.ToArray();
                clusters.Add(cluster);
            }
            result.clusterEntries = clusters.ToArray();

            var entries = new List<BiomeObjectConfig.ObjectEntry>();
            if (gen.Entries != null)
            foreach (var e in gen.Entries)
            {
                entries.Add(new BiomeObjectConfig.ObjectEntry
                {
                    mapObjectGuids = RuntimeConvert.ToGuidStrings(e.Prefabs, x => x.MapObjectGuid),
                    density = e.Density,
                    scaleRange = e.ScaleRange,
                    slopeAlignment = e.SlopeAlignment,
                    sinkRange = e.SinkRange,
                    noiseType = RuntimeConvert.ToMapNoiseType(e.NoiseType),
                    noiseFrequency = e.NoiseFrequency,
                    noiseAmplitude = e.NoiseAmplitude,
                    noiseThreshold = e.NoiseThreshold,
                    useSlopeFilter = e.UseSlopeFilter,
                    slopeMin = e.SlopeMin,
                    slopeMax = e.SlopeMax,
                    slopeSmoothness = e.SlopeSmoothness,
                    useClusterMode = e.UseClusterMode,
                    clusterCount = e.ClusterCount,
                    objectsPerCluster = e.ObjectsPerCluster,
                    clusterRadius = e.ClusterRadius,
                    minDistanceFromTree = e.MinDistanceFromTree,
                    maxDistanceFromTree = e.MaxDistanceFromTree
                });
            }
            result.entries = entries.ToArray();

            var a = gen.AlgorithmConfig;
            var ac = result.algorithmConfig;
            ac.heroOffsetFactor = a.HeroOffsetFactor;
            ac.heroScaleMinRatio = a.HeroScaleMinRatio;
            ac.heroScaleRange = a.HeroScaleRange;
            ac.heroYScaleMin = a.HeroYScaleMin;
            ac.heroYScaleRange = a.HeroYScaleRange;
            ac.subordinateDistMin = a.SubordinateDistMin;
            ac.subordinateDistRange = a.SubordinateDistRange;
            ac.subordinateAngleReject = a.SubordinateAngleReject;
            ac.subordinateScaleMaxRatio = a.SubordinateScaleMaxRatio;
            ac.subordinateYScaleMin = a.SubordinateYScaleMin;
            ac.subordinateYScaleRange = a.SubordinateYScaleRange;
            ac.saddleProbability = a.SaddleProbability;
            ac.saddleJitter = a.SaddleJitter;
            ac.biasSectorAngle = a.BiasSectorAngle;
            ac.rubbleSizeMin = a.RubbleSizeMin;
            ac.rubbleSizeRange = a.RubbleSizeRange;
            ac.rubbleDensityMultiplier = a.RubbleDensityMultiplier;
            ac.clusterSpacingFactor = a.ClusterSpacingFactor;

            return result;
        }
    }
}
