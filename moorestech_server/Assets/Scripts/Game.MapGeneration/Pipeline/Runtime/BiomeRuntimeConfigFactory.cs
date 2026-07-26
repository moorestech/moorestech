using Game.MapGeneration.Pipeline.Config;
using GenVanilla = Mooresmaster.Model.GenerationModule.VanillaGeneratorAlgorithmParam;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // 生成型の各バイオーム定義 → 実行時 *BiomeConfig POCO。高さ生成フィールドと配置サブ設定
    // (treePlacement/objectConfig) を写す。Alpine/Mesa は AlpineMesaRuntimeConfigFactory が担当。
    // Maps generated biome definitions to runtime *BiomeConfig POCOs (height fields plus
    // treePlacement/objectConfig). Alpine/Mesa are handled by AlpineMesaRuntimeConfigFactory.
    internal static class BiomeRuntimeConfigFactory
    {
        public static void Apply(TerrainGenerationConfig cfg, GenVanilla vp)
        {
            var g = vp.Grassland; var gc = cfg.grassland;
            gc.frequency = g.Frequency;
            gc.amplitude = g.Amplitude;
            gc.detailFrequency = g.DetailFrequency;
            gc.detailAmplitude = g.DetailAmplitude;
            gc.baseHeight = g.BaseHeight;
            gc.hillAmplitude = g.HillAmplitude;
            gc.terrainLayerAddressablePath = g.TerrainLayerAddressablePath;
            gc.treePlacement = TreeRuntimeConfigFactory.Build(g.TreePlacement);
            gc.objectConfig = ObjectRuntimeConfigFactory.Build(g.ObjectConfig);

            var f = vp.Forest; var fc = cfg.forest;
            fc.humidityThreshold = f.HumidityThreshold;
            fc.warpStrength = f.WarpStrength;
            fc.warpIterations = f.WarpIterations;
            fc.baseFrequency = f.BaseFrequency;
            fc.baseOctaves = f.BaseOctaves;
            fc.basePersistence = f.BasePersistence;
            fc.ridgeBlend = f.RidgeBlend;
            fc.ridgeOctaves = f.RidgeOctaves;
            fc.lowlandCutoff = f.LowlandCutoff;
            fc.exponent = f.Exponent;
            fc.plateauFlatten = f.PlateauFlatten;
            fc.detailFrequency = f.DetailFrequency;
            fc.detailOctaves = f.DetailOctaves;
            fc.detailWeight = f.DetailWeight;
            fc.baseHeight = f.BaseHeight;
            fc.amplitude = f.Amplitude;
            fc.terrainLayerAddressablePath = f.TerrainLayerAddressablePath;
            fc.treePlacement = TreeRuntimeConfigFactory.Build(f.TreePlacement);
            fc.objectConfig = ObjectRuntimeConfigFactory.Build(f.ObjectConfig);

            var s = vp.Savanna; var sc = cfg.savanna;
            sc.temperatureThreshold = s.TemperatureThreshold;
            sc.frequency = s.Frequency;
            sc.plateauFrequency = s.PlateauFrequency;
            sc.hillThreshold = s.HillThreshold;
            sc.plateauSharpness = s.PlateauSharpness;
            sc.undulationAmplitude = s.UndulationAmplitude;
            sc.baseHeight = s.BaseHeight;
            sc.amplitude = s.Amplitude;
            sc.terrainLayerAddressablePath = s.TerrainLayerAddressablePath;
            sc.treePlacement = TreeRuntimeConfigFactory.Build(s.TreePlacement);
            sc.objectConfig = ObjectRuntimeConfigFactory.Build(s.ObjectConfig);

            var d = vp.Desert; var dc = cfg.desert;
            dc.temperatureThreshold = d.TemperatureThreshold;
            dc.duneNoiseFrequency = d.DuneNoiseFrequency;
            dc.duneAmplitude = d.DuneAmplitude;
            dc.canyonDepth = d.CanyonDepth;
            dc.canyonFrequency = d.CanyonFrequency;
            dc.canyonOctaves = d.CanyonOctaves;
            dc.cliffAmplitude = d.CliffAmplitude;
            dc.cliffFrequency = d.CliffFrequency;
            dc.cliffOctaves = d.CliffOctaves;
            dc.absSmoothing = d.AbsSmoothing;
            dc.baseHeight = d.BaseHeight;
            dc.terrainLayerAddressablePath = d.TerrainLayerAddressablePath;
            dc.treePlacement = TreeRuntimeConfigFactory.Build(d.TreePlacement);
            dc.objectConfig = ObjectRuntimeConfigFactory.Build(d.ObjectConfig);

            var j = vp.Jungle; var jc = cfg.jungle;
            jc.temperatureThreshold = j.TemperatureThreshold;
            jc.humidityThreshold = j.HumidityThreshold;
            jc.warpStrength = j.WarpStrength;
            jc.warpOctaves = j.WarpOctaves;
            jc.terraceFrequency = j.TerraceFrequency;
            jc.terraceStepCount = j.TerraceStepCount;
            jc.transitionSmoothing = j.TransitionSmoothing;
            jc.cellHeightVariation = j.CellHeightVariation;
            jc.slopeWidth = j.SlopeWidth;
            jc.slopeRepeat = j.SlopeRepeat;
            jc.slopeCoverage = j.SlopeCoverage;
            jc.surfaceDetailFrequency = j.SurfaceDetailFrequency;
            jc.surfaceDetailAmplitude = j.SurfaceDetailAmplitude;
            jc.boundaryNoiseStrength = j.BoundaryNoiseStrength;
            jc.boundaryNoiseFrequency = j.BoundaryNoiseFrequency;
            jc.boundaryNoiseSlopeThreshold = j.BoundaryNoiseSlopeThreshold;
            jc.baseHeight = j.BaseHeight;
            jc.amplitude = j.Amplitude;
            jc.terrainLayerAddressablePath = j.TerrainLayerAddressablePath;
            jc.treePlacement = TreeRuntimeConfigFactory.Build(j.TreePlacement);
            jc.objectConfig = ObjectRuntimeConfigFactory.Build(j.ObjectConfig);

            var wd = vp.Woods; var wc = cfg.woods;
            wc.humidityThreshold = wd.HumidityThreshold;
            wc.humidityUpperThreshold = wd.HumidityUpperThreshold;
            wc.frequency = wd.Frequency;
            wc.terraceSteps = wd.TerraceSteps;
            wc.terraceSharpness = wd.TerraceSharpness;
            wc.baseHeight = wd.BaseHeight;
            wc.amplitude = wd.Amplitude;
            wc.terrainLayerAddressablePath = wd.TerrainLayerAddressablePath;
            wc.treePlacement = TreeRuntimeConfigFactory.Build(wd.TreePlacement);
            wc.objectConfig = ObjectRuntimeConfigFactory.Build(wd.ObjectConfig);
        }
    }
}
