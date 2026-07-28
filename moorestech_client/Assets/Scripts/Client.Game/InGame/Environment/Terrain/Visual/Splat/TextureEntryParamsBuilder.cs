using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat
{
    /// <summary>
    ///     SplatmapJob が食う TextureEntryParams を組み、BiomeParams へスライス情報を書き戻す。
    ///     MapMaking JobDataConverter.ConvertTextureEntries の移植で、レイヤー解決だけアドレス経由に置き換えた
    ///     Builds the TextureEntryParams SplatmapJob consumes and writes the slice info back into BiomeParams.
    ///     Ported from MapMaking's JobDataConverter.ConvertTextureEntries with layer lookup switched to addresses
    /// </summary>
    public static class TextureEntryParamsBuilder
    {
        // 移植元と同じseedオフセット。RNGの消費順まで一致させないとノイズ変調の分布がずれる
        // The same seed offset as the source; matching even the RNG consumption order keeps the noise modulation aligned
        private const int TextureNoiseSeedOffset = 77777;

        public static NativeArray<TextureEntryParams> Build(
            int worldSeed, BiomeTextureConfig[] biomeTextureConfigs,
            IReadOnlyDictionary<string, int> layerIndexByAddress,
            NativeArray<BiomeParams> biomeParams, Allocator allocator)
        {
            var textureRandom = new Random(worldSeed + TextureNoiseSeedOffset);

            var totalEntryCount = 0;
            foreach (var biomeTextureConfig in biomeTextureConfigs)
                totalEntryCount += biomeTextureConfig.entries.Length;

            // 0本だとNativeArrayが空になりSplatmapJobが落ちるため最低1本確保する（移植元と同じ）
            // A zero-length NativeArray would crash SplatmapJob, so at least one slot is reserved, as in the source
            var result = new NativeArray<TextureEntryParams>(Math.Max(totalEntryCount, 1), allocator);
            var cursor = 0;
            var globalNoiseIndex = 0;

            for (var biome = 0; biome < biomeTextureConfigs.Length; biome++)
            {
                var entries = biomeTextureConfigs[biome].entries;

                // どのスライスが自分のエントリかをBiomeParamsへ書き戻す
                // Write back which slice of the flat array belongs to this biome
                var parameters = biomeParams[biome];
                parameters.textureEntryBase = cursor;
                parameters.textureEntryCount = entries.Length;
                biomeParams[biome] = parameters;

                // 移植元にあるデッドコードをそのまま残している。textureRandomはこのメソッドのローカルで以降どこからも引かれず、
                // 消しても現在の出力は変わらない。移植元との差分を作らないためだけに保存している
                // Dead code preserved verbatim from the source: textureRandom is local to this method and never drawn from again,
                // so removing it would not change today's output. It is kept solely to avoid diverging from the source
                ConsumeOffsets(textureRandom, Math.Max(entries.Length * 4, 4));

                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                    result[cursor + entryIndex] = ToTextureEntryParams(entries[entryIndex], layerIndexByAddress, globalNoiseIndex + entryIndex);

                cursor += entries.Length;
                globalNoiseIndex += entries.Length;
            }

            return result;
        }

        private static TextureEntryParams ToTextureEntryParams(
            TextureEntry entry, IReadOnlyDictionary<string, int> layerIndexByAddress, int noiseOffsetIndex)
        {
            // SplatLayerTableが全アドレスを登録済みなので未登録は表の組み立て漏れ。0番へ黙って倒さない
            // SplatLayerTable registered every address, so a miss means the table was built wrong; never fall back to index 0
            if (!layerIndexByAddress.TryGetValue(entry.layerAddressablePath, out var layerIndex))
                throw new InvalidOperationException(
                    $"[TextureEntryParamsBuilder] Layer address '{entry.layerAddressablePath}' is missing from the splat layer table.");

            return new TextureEntryParams
            {
                layerIndex = layerIndex,
                weight = entry.weight,

                useSlopeFilter = entry.useSlopeFilter ? 1 : 0,
                slopeMin = entry.slopeMin,
                slopeMax = entry.slopeMax,
                slopeSmoothness = entry.slopeSmoothness,

                useHeightFilter = entry.useHeightFilter ? 1 : 0,
                heightMin = entry.heightMin,
                heightMax = entry.heightMax,
                heightSmoothness = entry.heightSmoothness,

                useCurvatureFilter = entry.useCurvatureFilter ? 1 : 0,
                curvatureMin = entry.curvatureMin,
                curvatureMax = entry.curvatureMax,
                curvatureSmoothness = entry.curvatureSmoothness,

                noiseType = (int)entry.noiseType,
                noiseFrequency = entry.noiseFrequency,
                noiseAmplitude = entry.noiseAmplitude,
                noiseOffsetIndex = noiseOffsetIndex,
            };
        }

        private static void ConsumeOffsets(Random random, int count)
        {
            for (var i = 0; i < count; i++)
            {
                random.NextDouble();
                random.NextDouble();
            }
        }
    }
}
