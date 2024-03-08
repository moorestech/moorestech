using ClassLibrary;
using Core.Ore;
using UnityEngine;

namespace Game.WorldMap
{
    /// <summary>
    ///     鉱脈を生成し、その座標の鉱石を返す
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

        public int /**/GetOreId(Vector2Int pos)
        {
            var ids = _oreConfig.GetSortedIdsForPriority();
            foreach (var id in ids)
            {
                var config = _oreConfig.Get(id);
                if (ExistsVein(pos, config.VeinSize, config.VeinFrequency, id, _seed)) return id;
            }

            return OreConst.NoneOreId;
        }

        /// <summary>
        ///     cellularノイズから鉱脈を生成し、そこに鉱石があるかを判定する
        /// </summary>
        /// <param name="veinSize">鉱脈の大きさ</param>
        /// <param name="veinFrequency">鉱脈の頻度</param>
        private bool ExistsVein(Vector2Int pos, float veinSize, float veinFrequency, int oreId, int seed)
        {
            //ノイズの設定
            var noise = new FastNoiseLite(seed);
            noise.SetFrequency(veinFrequency * DefaultVeinFrequency);
            noise.SetSeed(seed + oreId);

            noise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
            noise.SetCellularDistanceFunction(FastNoiseLite.CellularDistanceFunction.Euclidean);
            noise.SetCellularReturnType(FastNoiseLite.CellularReturnType.Distance);
            noise.SetCellularJitter(1.0f);

            //ノイズの値を取得
            var x = pos.x;
            var y = pos.y;
            var noiseValue = noise.GetNoise(x, y);
            //鉱脈の大きさを実際にノイズで使う値に変換
            var useVeinsSize = DefaultVeinsSize + (veinSize - 1) * 0.01f;

            return noiseValue < useVeinsSize;
        }
    }
}