using System;
using Core.Master.Validator;
using Game.MapGeneration.Pipeline.Config;
using GenVanilla = Mooresmaster.Model.GenerationModule.VanillaGeneratorAlgorithmParam;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // 全バイオームのobjectConfigをカタログの1列から転写する。バイオームを増やしたとき、
    // マスタ検証とここが同じ列を見るため、片方だけ無言で漏れることがない。
    // Transcribes every biome's objectConfig from the one catalog list; because master validation reads the same list,
    // adding a biome cannot slip silently past just one of them.
    internal static class BiomeObjectConfigRuntimeApplier
    {
        public static void Apply(TerrainGenerationConfig cfg, GenVanilla vp)
        {
            foreach (var (biomeName, objectConfig) in GenerationBiomeObjectConfigCatalog.Of(vp))
            {
                var built = ObjectRuntimeConfigFactory.Build(objectConfig);
                switch (biomeName)
                {
                    case "grassland": cfg.grassland.objectConfig = built; break;
                    case "forest": cfg.forest.objectConfig = built; break;
                    case "savanna": cfg.savanna.objectConfig = built; break;
                    case "desert": cfg.desert.objectConfig = built; break;
                    case "jungle": cfg.jungle.objectConfig = built; break;
                    case "woods": cfg.woods.objectConfig = built; break;
                    case "alpine": cfg.alpine.objectConfig = built; break;
                    case "mesa": cfg.mesa.objectConfig = built; break;
                    // カタログへ足したバイオームをここへ足し忘れたら、配置設定を黙って捨てずに止める
                    // A biome added to the catalog but not here stops the load instead of dropping its placement config
                    default: throw new InvalidOperationException($"[GenerationMaster] biome {biomeName} has no runtime objectConfig target");
                }
            }
        }
    }
}
