using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using GenObject = Mooresmaster.Model.BiomeObjectConfigModule.BiomeObjectConfig;
using GenScatterParam = Mooresmaster.Model.BiomeObjectConfigModule.ScatterPlacementParam;
using GenClusterParam = Mooresmaster.Model.BiomeObjectConfigModule.ClusterPlacementParam;

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
                // 主配置候補を順序どおり写し、空GUIDを生成処理へ渡さない。
                // Copy primary candidates in order and keep empty GUIDs out of generation.
                var primaryGuids = new string[ce.Primary.Length];
                for (var i = 0; i < ce.Primary.Length; i++)
                    primaryGuids[i] = RuntimeConvert.ToRequiredGuidString(
                        ce.Primary[i].MapObjectGuid,
                        "objectConfig.clusterEntries.primary.mapObjectGuid");

                var cluster = new ObjectClusterEntry
                {
                    primary = primaryGuids,
                    terrainSurroundEffectType = RuntimeConvert.ToTerrainSurroundEffectType(ce.TerrainSurroundEffectType, "objectConfig.clusterEntries.terrainSurroundEffectType"),
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
                    // 従属配置候補も同じ必須GUID規約で変換する。
                    // Convert secondary candidates under the same required-GUID contract.
                    var secondaryGuids = new string[s.Prefabs.Length];
                    for (var i = 0; i < s.Prefabs.Length; i++)
                        secondaryGuids[i] = RuntimeConvert.ToRequiredGuidString(
                            s.Prefabs[i].MapObjectGuid,
                            "objectConfig.clusterEntries.secondaries.prefabs.mapObjectGuid");

                    secondaries.Add(new ObjectClusterSecondary
                    {
                        mode = RuntimeConvert.ToSecondaryMode(s.Mode),
                        mapObjectGuids = secondaryGuids,
                        terrainSurroundEffectType = RuntimeConvert.ToTerrainSurroundEffectType(s.TerrainSurroundEffectType, "objectConfig.clusterEntries.secondaries.terrainSurroundEffectType"),
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
                // 独立散布候補も空GUIDを拒否してから実行時設定へ渡す。
                // Reject empty independent-scatter GUIDs before placing them in runtime config.
                var entryGuids = new string[e.Prefabs.Length];
                for (var i = 0; i < e.Prefabs.Length; i++)
                    entryGuids[i] = RuntimeConvert.ToRequiredGuidString(
                        e.Prefabs[i].MapObjectGuid,
                        "objectConfig.entries.prefabs.mapObjectGuid");

                entries.Add(new BiomeObjectConfig.ObjectEntry
                {
                    mapObjectGuids = entryGuids,
                    terrainSurroundEffectType = RuntimeConvert.ToTerrainSurroundEffectType(e.TerrainSurroundEffectType, "objectConfig.entries.terrainSurroundEffectType"),
                    placement = e.PlacementParam is GenClusterParam genCluster
                        ? BuildCluster(genCluster)
                        : BuildScatter((GenScatterParam)e.PlacementParam),
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

            #region Internal

            // 配置方式ごとのパラメータを、bandsの並び順を保ったまま実行時型へ写す。
            // Transcribes the per-mode placement parameters into runtime types, keeping the band order.
            ObjectPlacementParam BuildScatter(GenScatterParam genScatter)
            {
                var scatterBands = new ObjectScatterBand[genScatter.Bands.Length];
                for (var i = 0; i < genScatter.Bands.Length; i++)
                    scatterBands[i] = new ObjectScatterBand
                    {
                        outerRadiusMeters = genScatter.Bands[i].OuterRadiusMeters,
                        pointsPerHectare = genScatter.Bands[i].PointsPerHectare
                    };
                return new ObjectScatterParam { bands = scatterBands };
            }

            ObjectPlacementParam BuildCluster(GenClusterParam genCluster)
            {
                var clusterBands = new ObjectClusterBand[genCluster.Bands.Length];
                for (var i = 0; i < genCluster.Bands.Length; i++)
                    clusterBands[i] = new ObjectClusterBand
                    {
                        outerRadiusMeters = genCluster.Bands[i].OuterRadiusMeters,
                        clusterCentersPerHectare = genCluster.Bands[i].ClusterCentersPerHectare
                    };
                return new ObjectClusterParam
                {
                    bands = clusterBands,
                    objectsPerCluster = genCluster.ObjectsPerCluster,
                    clusterRadius = genCluster.ClusterRadius
                };
            }

            #endregion
        }
    }
}
