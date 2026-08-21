using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using GenTextureConfig = Mooresmaster.Model.BiomeTextureConfigModule.BiomeTextureConfig;

namespace Game.MapGeneration.Pipeline.Visual.Splat
{
    /// <summary>
    ///     GenerationMaster の biomeTextureConfig を実行時 POCO へ変換する。レイヤーはアドレス文字列のまま運ぶ
    ///     Converts GenerationMaster's biomeTextureConfig into runtime POCOs, carrying layers as address strings
    /// </summary>
    public static class SplatTextureConfigFactory
    {
        public static BiomeTextureConfig Build(GenTextureConfig generated)
        {
            var entries = new List<TextureEntry>();
            foreach (var generatedEntry in generated.Entries)
                entries.Add(ToTextureEntry(generatedEntry));

            return new BiomeTextureConfig { entries = entries.ToArray() };
        }

        private static TextureEntry ToTextureEntry(Mooresmaster.Model.BiomeTextureConfigModule.EntriesElement generated)
        {
            return new TextureEntry
            {
                layerAddressablePath = generated.LayerAddressablePath,
                weight = generated.Weight,

                useSlopeFilter = generated.UseSlopeFilter,
                slopeMin = generated.SlopeMin,
                slopeMax = generated.SlopeMax,
                slopeSmoothness = generated.SlopeSmoothness,

                useHeightFilter = generated.UseHeightFilter,
                heightMin = generated.HeightMin,
                heightMax = generated.HeightMax,
                heightSmoothness = generated.HeightSmoothness,

                useCurvatureFilter = generated.UseCurvatureFilter,
                curvatureMin = generated.CurvatureMin,
                curvatureMax = generated.CurvatureMax,
                curvatureSmoothness = generated.CurvatureSmoothness,

                noiseType = ParseNoiseType(generated.NoiseType),
                noiseFrequency = generated.NoiseFrequency,
                noiseAmplitude = generated.NoiseAmplitude,
            };
        }

        // Mooresmaster は enum をオプション名の文字列で生成する。未知名は既定化せず違反名を添えて落とす
        // Mooresmaster emits enums as option-name strings; an unknown name fails loud with the offending value
        private static MapNoiseType ParseNoiseType(string generatedName)
        {
            if (Enum.TryParse<MapNoiseType>(generatedName, out var parsed)) return parsed;
            throw new InvalidOperationException(
                $"[SplatTextureConfigFactory] 'noiseType' has an unrecognized enum value: '{generatedName}'.");
        }
    }
}
