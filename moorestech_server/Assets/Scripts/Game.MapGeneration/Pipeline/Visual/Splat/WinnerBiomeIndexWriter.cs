using System;
using Game.MapGeneration.Pipeline.Biomes;
using Unity.Collections;

namespace Game.MapGeneration.Pipeline.Visual.Splat
{
    /// <summary>
    ///     再計算したwinnerBiomeIndexを、Ocean/Beach折り込み済みのbiomeバイト列で上書きする
    ///     Overwrites the recomputed winnerBiomeIndex with the Ocean/Beach-folded biome bytes
    /// </summary>
    public static class WinnerBiomeIndexWriter
    {
        private const int UnmappedBiomeType = -2;

        // SplatmapJobは winner < 0 を海ピクセルとして扱う
        // SplatmapJob treats winner < 0 as a sea pixel
        private const int SeaWinnerIndex = -1;

        public static void Overwrite(
            NativeArray<int> winnerBiomeIndex, byte[,] biomeIndices, BiomeType[] biomeTypes, int resolution)
        {
            var winnerIndexByBiomeTypeValue = BuildLookup(biomeTypes);

            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var biomeType = biomeIndices[z, x];

                // BeachだけはbiomeIndicesに答えが無い。PlacementInputBuilder.BuildBiomeIndices が
                // beachFactor>0.2 のピクセルを Beach で塗り潰し、元のwinnerを捨てているため。
                // ここだけ再計算値を残すのは一貫性の欠如ではなく、biomeIndices側が情報を持たないことへの対応
                // Beach alone has no answer in biomeIndices: PlacementInputBuilder.BuildBiomeIndices
                // overwrites pixels with beachFactor>0.2 as Beach and discards the original winner.
                // Keeping the recomputed value here is not an inconsistency but the only source that still knows
                if (biomeType == (byte)BiomeType.Beach) continue;

                var pixelIndex = z * resolution + x;
                if (biomeType == (byte)BiomeType.Ocean)
                {
                    winnerBiomeIndex[pixelIndex] = SeaWinnerIndex;
                    continue;
                }

                // 有効バイオーム一覧に無い値は、サーバーとクライアントで有効バイオームが食い違っている証拠
                // A value outside the enabled biome list proves the server and client disagree on which biomes are enabled
                var mappedWinner = winnerIndexByBiomeTypeValue[biomeType];
                if (mappedWinner == UnmappedBiomeType)
                    throw new InvalidOperationException(
                        $"[WinnerBiomeIndexWriter] Biome '{(BiomeType)biomeType}' is not among the enabled biomes.");

                winnerBiomeIndex[pixelIndex] = mappedWinner;
            }
        }

        private static int[] BuildLookup(BiomeType[] biomeTypes)
        {
            var winnerIndexByBiomeTypeValue = new int[byte.MaxValue + 1];
            for (var i = 0; i < winnerIndexByBiomeTypeValue.Length; i++)
                winnerIndexByBiomeTypeValue[i] = UnmappedBiomeType;

            for (var i = 0; i < biomeTypes.Length; i++)
                winnerIndexByBiomeTypeValue[(byte)biomeTypes[i]] = i;

            return winnerIndexByBiomeTypeValue;
        }
    }
}
