using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using GenTree = Mooresmaster.Model.TreePlacementConfigModule.TreePlacementConfig;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // 生成型 treePlacementConfig → 実行時 TreePlacementConfig POCO。prototypes 以下の入れ子は
    // すべて var で辿り、prefab 参照は mapObjectGuid 文字列配列へ写す。
    // Converts generated treePlacementConfig to the runtime TreePlacementConfig POCO; deeper
    // nesting is traversed via var, and prefab references become mapObjectGuid strings.
    internal static class TreeRuntimeConfigFactory
    {
        public static TreePlacementConfig Build(GenTree gen)
        {
            var result = new TreePlacementConfig();
            if (gen?.Prototypes == null) return result;

            var protos = new List<TreePrototypeEntry>();
            foreach (var p in gen.Prototypes)
            {
                var entry = new TreePrototypeEntry
                {
                    mapObjectGuids = RuntimeConvert.ToGuidStrings(p.MapObjects, mo => mo.MapObjectGuid),
                    scaleHeightRange = p.ScaleHeightRange,
                    scaleWidthRange = p.ScaleWidthRange,
                    lockWidthHeight = p.LockWidthHeight,
                    sink = p.Sink,
                    bendFactor = p.BendFactor,
                    randomRotation = p.RandomRotation,
                    disabled = p.Disabled,
                    borderMargin = p.BorderMargin,
                    sharedGridMinDistance = p.SharedGridMinDistance,
                    clusterNoiseThreshold = p.ClusterNoiseThreshold,
                    noise2Op = RuntimeConvert.ToNoiseOp(p.Noise2Op),
                    heightModAmount = p.HeightModAmount,
                    heightModWidth = p.HeightModWidth,
                    boundaryScaleMultiplier = p.BoundaryScaleMultiplier,
                    oldGrowthScale = p.OldGrowthScale,
                    oldGrowthRatio = p.OldGrowthRatio,
                    slopeFilter = PlacementRefConvert.ToPlacementFilter(p.SlopeFilter),
                    curvatureFilter = PlacementRefConvert.ToPlacementFilter(p.CurvatureFilter),
                    clusterNoise = PlacementRefConvert.ToPlacementNoise(p.ClusterNoise),
                    clusterNoise2 = PlacementRefConvert.ToPlacementNoise(p.ClusterNoise2)
                };

                var d = p.DensityConfig;
                var dc = entry.densityConfig;
                dc.denseMinThreshold = d.DenseMinThreshold;
                dc.transitionMinThreshold = d.TransitionMinThreshold;
                dc.densePassMinDistance = d.DensePassMinDistance;
                dc.transitionPassMinDistance = d.TransitionPassMinDistance;
                dc.sparsePassMinDistance = d.SparsePassMinDistance;
                dc.scatterPassMinDistance = d.ScatterPassMinDistance;
                dc.transitionBaseProb = d.TransitionBaseProb;
                dc.transitionPeakProb = d.TransitionPeakProb;
                dc.transitionProbPower = d.TransitionProbPower;
                dc.sparseOpenRejectFactor = d.SparseOpenRejectFactor;
                dc.scatterBaseProb = d.ScatterBaseProb;
                dc.scatterDensityFactor = d.ScatterDensityFactor;
                dc.slopeHardReject = d.SlopeHardReject;
                dc.slopeSoftReject = d.SlopeSoftReject;
                dc.rockRejectDistance = d.RockRejectDistance;
                dc.rockRejectProb = d.RockRejectProb;
                dc.rockBoostNearDistance = d.RockBoostNearDistance;
                dc.rockBoostFarDistance = d.RockBoostFarDistance;
                dc.rockFarRejectProb = d.RockFarRejectProb;
                dc.densityLargeFrequency = d.DensityLargeFrequency;
                dc.densityMidFrequency = d.DensityMidFrequency;
                dc.densitySmallFrequency = d.DensitySmallFrequency;
                dc.densityLargeWeight = d.DensityLargeWeight;
                dc.densityMidWeight = d.DensityMidWeight;
                dc.densitySmallWeight = d.DensitySmallWeight;
                dc.densityFloor = d.DensityFloor;
                dc.islandModulationFrequency = d.IslandModulationFrequency;
                dc.islandModulationMin = d.IslandModulationMin;
                dc.islandModulationMax = d.IslandModulationMax;
                dc.canopyScaleThreshold = d.CanopyScaleThreshold;
                dc.densePassMultiplier = d.DensePassMultiplier;
                dc.transitionPassMultiplier = d.TransitionPassMultiplier;
                dc.sparsePassMultiplier = d.SparsePassMultiplier;
                dc.densityModMin = d.DensityModMin;
                dc.densityModMax = d.DensityModMax;
                dc.densityModScale = d.DensityModScale;
                dc.keepProbNear = d.KeepProbNear;
                dc.keepProbFar = d.KeepProbFar;
                dc.localDensityCapRadius = d.LocalDensityCapRadius;
                dc.localDensityCapCount = d.LocalDensityCapCount;

                var u = p.UnderstoryConfig;
                var uc = entry.understoryConfig;
                uc.understoryScaleThreshold = u.UnderstoryScaleThreshold;
                uc.understoryNeighborRadius = u.UnderstoryNeighborRadius;
                uc.densePatches = u.DensePatches;
                uc.densePatchesRandom = u.DensePatchesRandom;
                uc.transitionPatches = u.TransitionPatches;
                uc.transitionPatchesRandom = u.TransitionPatchesRandom;
                uc.denseTreesPerCanopy = u.DenseTreesPerCanopy;
                uc.denseTreesRandom = u.DenseTreesRandom;
                uc.transitionTreesPerCanopy = u.TransitionTreesPerCanopy;
                uc.transitionTreesRandom = u.TransitionTreesRandom;
                uc.patchDistanceMin = u.PatchDistanceMin;
                uc.patchDistanceMax = u.PatchDistanceMax;
                uc.densePatchRadiusMin = u.DensePatchRadiusMin;
                uc.densePatchRadiusMax = u.DensePatchRadiusMax;
                uc.transitionPatchRadiusMin = u.TransitionPatchRadiusMin;
                uc.transitionPatchRadiusMax = u.TransitionPatchRadiusMax;
                uc.denseMaskThreshold = u.DenseMaskThreshold;
                uc.transitionMaskThreshold = u.TransitionMaskThreshold;
                uc.understorySlopeLimit = u.UnderstorySlopeLimit;
                uc.scatterMinDistance = u.ScatterMinDistance;
                uc.scatterDensityMultiplier = u.ScatterDensityMultiplier;
                uc.scatterProbMin = u.ScatterProbMin;
                uc.scatterProbMax = u.ScatterProbMax;
                uc.scatterSlopeLimit = u.ScatterSlopeLimit;
                uc.scatterClusterSize = u.ScatterClusterSize;
                uc.scatterClusterSizeRandom = u.ScatterClusterSizeRandom;
                uc.scatterClusterRadiusMin = u.ScatterClusterRadiusMin;
                uc.scatterClusterRadiusRandom = u.ScatterClusterRadiusRandom;
                uc.scatterNeighborRadius = u.ScatterNeighborRadius;
                uc.patchAspectMin = u.PatchAspectMin;
                uc.patchAspectMax = u.PatchAspectMax;
                uc.scatterAspectMin = u.ScatterAspectMin;
                uc.scatterAspectMax = u.ScatterAspectMax;
                uc.patchMaskFrequency = u.PatchMaskFrequency;
                uc.patchMaskWeight = u.PatchMaskWeight;
                uc.patchMaskEllipseOffset = u.PatchMaskEllipseOffset;
                uc.patchTargetDense = u.PatchTargetDense;
                uc.patchTargetDenseRandom = u.PatchTargetDenseRandom;
                uc.patchTargetTransition = u.PatchTargetTransition;
                uc.patchTargetTransitionRandom = u.PatchTargetTransitionRandom;
                uc.edgeScaleMax = u.EdgeScaleMax;
                uc.edgeScaleMin = u.EdgeScaleMin;
                uc.scatterMaskFrequency = u.ScatterMaskFrequency;
                uc.scatterMaskBlendMin = u.ScatterMaskBlendMin;
                uc.scatterMaskBlendMax = u.ScatterMaskBlendMax;

                var r = p.RockProximityConfig;
                var rc = entry.rockProximityConfig;
                rc.enabled = r.Enabled;
                rc.patchCountMin = r.PatchCountMin;
                rc.patchCountRandom = r.PatchCountRandom;
                rc.patchDistanceMin = r.PatchDistanceMin;
                rc.patchDistanceRandom = r.PatchDistanceRandom;
                rc.patchSizeMin = r.PatchSizeMin;
                rc.patchSizeRandom = r.PatchSizeRandom;
                rc.maskThresholdMin = r.MaskThresholdMin;
                rc.maskThresholdRandom = r.MaskThresholdRandom;
                rc.attemptsMin = r.AttemptsMin;
                rc.attemptsRandom = r.AttemptsRandom;
                rc.scaleLowBase = r.ScaleLowBase;
                rc.scaleLowRange = r.ScaleLowRange;
                rc.scaleHighBase = r.ScaleHighBase;
                rc.scaleHighRange = r.ScaleHighRange;
                rc.maskCoarseFrequency = r.MaskCoarseFrequency;
                rc.maskFineFrequency = r.MaskFineFrequency;
                rc.maskCoarseWeight = r.MaskCoarseWeight;
                rc.distancePenaltyFactor = r.DistancePenaltyFactor;
                protos.Add(entry);
            }
            result.prototypes = protos.ToArray();
            return result;
        }
    }
}
