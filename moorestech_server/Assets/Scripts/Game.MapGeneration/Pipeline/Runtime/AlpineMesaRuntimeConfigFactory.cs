using Game.MapGeneration.Pipeline.Config;
using GenVanilla = Mooresmaster.Model.GenerationModule.VanillaGeneratorAlgorithmParam;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // Alpine/Mesa は台地検出・段彩フィールドが多いため BiomeRuntimeConfigFactory から分離した。
    // Alpine/Mesa carry many plateau/terrace fields, so they are split out from BiomeRuntimeConfigFactory.
    internal static class AlpineMesaRuntimeConfigFactory
    {
        public static void Apply(TerrainGenerationConfig cfg, GenVanilla vp)
        {
            var a = vp.Alpine; var ac = cfg.alpine;
            ac.elevationThreshold = a.ElevationThreshold;
            ac.warpStrength = a.WarpStrength;
            ac.warpIterations = a.WarpIterations;
            ac.frequency = a.Frequency;
            ac.octaves = a.Octaves;
            ac.ridgeBlend = a.RidgeBlend;
            ac.ridgeOctaves = a.RidgeOctaves;
            ac.exponent = a.Exponent;
            ac.ceilHeight = a.CeilHeight;
            ac.ceilStrength = a.CeilStrength;
            ac.floorHeight = a.FloorHeight;
            ac.floorStrength = a.FloorStrength;
            ac.baseHeight = a.BaseHeight;
            ac.amplitude = a.Amplitude;
            ac.enablePlateau = a.EnablePlateau;
            ac.prominenceThreshold = a.ProminenceThreshold;
            ac.minProminentDirections = a.MinProminentDirections;
            ac.plateauSearchBaseRadius = a.PlateauSearchBaseRadius;
            ac.plateauBoundaryBlend = a.PlateauBoundaryBlend;
            ac.minRegionSize = a.MinRegionSize;
            ac.minPlateauCoverage = a.MinPlateauCoverage;
            ac.coverageTolerance = a.CoverageTolerance;
            ac.plateauBaseTransition = a.PlateauBaseTransition;
            ac.plateauTransitionScale = a.PlateauTransitionScale;
            ac.smoothRadius = a.SmoothRadius;
            ac.smoothIterations = a.SmoothIterations;
            ac.boundaryInnerBand = a.BoundaryInnerBand;
            ac.boundaryOuterBand = a.BoundaryOuterBand;
            ac.boundaryGaussSigma = a.BoundaryGaussSigma;
            ac.boundaryNoiseFrequency = a.BoundaryNoiseFrequency;
            ac.boundaryNoiseAmplitude = a.BoundaryNoiseAmplitude;
            ac.boundaryNoiseOctaves = a.BoundaryNoiseOctaves;
            ac.boundaryRefineIterations = a.BoundaryRefineIterations;
            ac.debugPlateauOverlay = a.DebugPlateauOverlay;
            ac.debugTerrainLayerAddressablePaths = a.DebugTerrainLayerAddressablePaths;
            ac.terrainLayerAddressablePath = a.TerrainLayerAddressablePath;
            ac.treePlacement = TreeRuntimeConfigFactory.Build(a.TreePlacement);
            ac.objectConfig = ObjectRuntimeConfigFactory.Build(a.ObjectConfig);

            var m = vp.Mesa; var mc = cfg.mesa;
            mc.elevationThreshold = m.ElevationThreshold;
            mc.humidityThreshold = m.HumidityThreshold;
            mc.warpStrength = m.WarpStrength;
            mc.warpIterations = m.WarpIterations;
            mc.frequency = m.Frequency;
            mc.octaves = m.Octaves;
            mc.persistence = m.Persistence;
            mc.isolationFreqMult = m.IsolationFreqMult;
            mc.canyonDepth = m.CanyonDepth;
            mc.canyonFreqMult = m.CanyonFreqMult;
            mc.canyonOctaves = m.CanyonOctaves;
            mc.boundaryNoiseStrength = m.BoundaryNoiseStrength;
            mc.boundaryNoiseFreqMult = m.BoundaryNoiseFreqMult;
            mc.boundaryNoiseOctaves = m.BoundaryNoiseOctaves;
            mc.butteThreshold = m.ButteThreshold;
            mc.cliffSteepness = m.CliffSteepness;
            mc.plateauFlatten = m.PlateauFlatten;
            mc.terraceSteps = m.TerraceSteps;
            mc.terraceSharpness = m.TerraceSharpness;
            mc.floorVariation = m.FloorVariation;
            mc.topNoiseStrength = m.TopNoiseStrength;
            mc.topNoiseFreqMult = m.TopNoiseFreqMult;
            mc.baseHeight = m.BaseHeight;
            mc.amplitude = m.Amplitude;
            mc.terrainLayerAddressablePath = m.TerrainLayerAddressablePath;
            mc.treePlacement = TreeRuntimeConfigFactory.Build(m.TreePlacement);
            mc.objectConfig = ObjectRuntimeConfigFactory.Build(m.ObjectConfig);
        }
    }
}
