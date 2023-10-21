using Core.Ore;

namespace Game.WorldMap
{
    /// <summary>
    ///     
    /// </summary>
    public class VeinGenerator
    {
        private const float DefaultVeinsSize = -0.97f;
        private const float DefaultVeinFrequency = 0.005f;
        private readonly IOreConfig _oreConfig;

        private readonly int _seed;

        public VeinGenerator(Seed seed, IOreConfig oreConfig)
        {
            _seed = seed.SeedValue;
            _oreConfig = oreConfig;
        }

        public int GetOreId(int x, int y)
        {
            var ids = _oreConfig.GetSortedIdsForPriority();
            foreach (var id in ids)
            {
                var config = _oreConfig.Get(id);
                if (ExistsVein(x, y, config.VeinSize, config.VeinFrequency, id, _seed)) return id;
            }

            return OreConst.NoneOreId;
        }


        ///     cellular

        /// <param name="veinSize"></param>
        /// <param name="veinFrequency"></param>
        private bool ExistsVein(int x, int y, float veinSize, float veinFrequency, int oreId, int seed)
        {
            
            var noise = new FastNoiseLite(seed);
            noise.SetFrequency(veinFrequency * DefaultVeinFrequency);
            noise.SetSeed(seed + oreId);

            noise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
            noise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
            noise.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
            noise.SetCellularJitter(1.0f);

            
            var noiseValue = noise.GetNoise(x, y);
            
            var useVeinsSize = DefaultVeinsSize + (veinSize - 1) * 0.01f;

            return noiseValue < useVeinsSize;
        }
    }
}