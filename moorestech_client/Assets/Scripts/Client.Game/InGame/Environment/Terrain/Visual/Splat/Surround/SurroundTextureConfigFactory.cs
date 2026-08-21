using GenObjectConfig = Mooresmaster.Model.BiomeObjectConfigModule.BiomeObjectConfig;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     GenerationMaster の objectConfig.surroundTextureConfig を実行時POCOへ写す。
    ///     レイヤーはアドレス文字列のまま運び、実アセットの解決は SplatLayerTable の並びに任せる
    ///     Copies GenerationMaster's objectConfig.surroundTextureConfig into the runtime POCO, carrying the
    ///     layer as an address string and leaving asset resolution to SplatLayerTable's ordering
    /// </summary>
    public static class SurroundTextureConfigFactory
    {
        public static SurroundTextureConfig Build(GenObjectConfig generatedObjectConfig)
        {
            var generated = generatedObjectConfig.SurroundTextureConfig;

            return new SurroundTextureConfig
            {
                enabled = generated.Enabled,
                surroundLayerAddressablePath = generated.SurroundLayerAddressablePath,

                coreRadius = generated.CoreRadius,
                coreBlendMin = generated.CoreBlendMin,
                coreBlendMax = generated.CoreBlendMax,

                transitionRadius = generated.TransitionRadius,
                transitionBlendMin = generated.TransitionBlendMin,
                transitionBlendMax = generated.TransitionBlendMax,

                noiseLowFrequency = generated.NoiseLowFrequency,
                noiseHighFrequency = generated.NoiseHighFrequency,
                noiseLowWeight = generated.NoiseLowWeight,

                rockMeshBaseSize = generated.RockMeshBaseSize,

                singleRockRadius = generated.SingleRockRadius,
                singleRockBlend = generated.SingleRockBlend,
            };
        }
    }
}
