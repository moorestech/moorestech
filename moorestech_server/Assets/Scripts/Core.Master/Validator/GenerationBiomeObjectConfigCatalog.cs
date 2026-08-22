using Mooresmaster.Model.BiomeObjectConfigModule;
using Mooresmaster.Model.GenerationModule;

namespace Core.Master.Validator
{
    // 生成型はバイオームごとに別クラスで共通の型を持たないため、名前を添えた1列にするのはここだけとする。
    // バイオームを増やすときの変更点をこの1箇所へ集め、帯検証や実行時転写が片側だけ漏れるのを防ぐ。
    // The generated model gives each biome its own class with no shared type, so this is the only place they are lined up with their names.
    // Adding a biome changes just this list, so band validation and runtime transcription cannot fall out of sync.
    public static class GenerationBiomeObjectConfigCatalog
    {
        public static (string BiomeName, BiomeObjectConfig ObjectConfig)[] Of(VanillaGeneratorAlgorithmParam vanillaGenerator)
        {
            return new[]
            {
                ("grassland", vanillaGenerator.Grassland.ObjectConfig),
                ("forest", vanillaGenerator.Forest.ObjectConfig),
                ("savanna", vanillaGenerator.Savanna.ObjectConfig),
                ("desert", vanillaGenerator.Desert.ObjectConfig),
                ("jungle", vanillaGenerator.Jungle.ObjectConfig),
                ("woods", vanillaGenerator.Woods.ObjectConfig),
                ("alpine", vanillaGenerator.Alpine.ObjectConfig),
                ("mesa", vanillaGenerator.Mesa.ObjectConfig),
            };
        }
    }
}
