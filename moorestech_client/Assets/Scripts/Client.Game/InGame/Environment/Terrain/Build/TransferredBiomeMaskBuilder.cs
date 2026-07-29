using Game.MapGeneration.Pipeline.Biomes;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     転送済みバイオームバイト列から1バイオームのwinnerマスクを作る。MapMaking BiomeMaskBuilder の置き換えで、
    ///     再計算した重みではなくサーバーが確定させた勝者をそのまま使う
    ///     Builds one biome's winner mask from the transferred biome bytes, replacing MapMaking's BiomeMaskBuilder
    ///     by taking the server's decided winner instead of locally recomputed weights
    /// </summary>
    public static class TransferredBiomeMaskBuilder
    {
        // 転送バイトはPlacementInputBuilder.BuildBiomeIndicesが「海→Ocean / ビーチ→Beach / それ以外→勝者バイオーム」で確定させた値
        // The transferred byte is what PlacementInputBuilder.BuildBiomeIndices decided: Ocean for sea, Beach for shore, else the winner biome
        public static bool[,] Build(byte[,] transferredBiomeIndices, BiomeType biomeType, int resolution)
        {
            var mask = new bool[resolution, resolution];
            var biomeValue = (byte)biomeType;

            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                mask[z, x] = transferredBiomeIndices[z, x] == biomeValue;

            return mask;
        }
    }
}
