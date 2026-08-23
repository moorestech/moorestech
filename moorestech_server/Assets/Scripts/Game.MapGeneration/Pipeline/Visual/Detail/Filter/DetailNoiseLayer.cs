using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Detail.Filter
{
    /// <summary>
    ///     単一ノイズレイヤー。MapMaking DetailNoiseLayer の移植で、Detail の密度とフィルタを変調する
    ///     Single noise layer ported from MapMaking's DetailNoiseLayer, modulating detail density and filters
    /// </summary>
    public class DetailNoiseLayer
    {
        public MapNoiseType noiseType;
        public float frequency;
        public float amplitude;

        // 出力に加算してからClampする。分布の底上げ・底下げに使う
        // Added to the output before clamping, raising or lowering the whole distribution
        public float offset;

        // 0.5中心のバランス調整。正で明部寄り、負で暗部寄り
        // Balance around 0.5: positive favors the bright side, negative the dark side
        public float balance;

        public bool IsActive => noiseType != MapNoiseType.None;

        // amplitude→balance→offsetの順で適用しClampする。PlacementNoiseとは適用順もClampの有無も異なる
        // Applies amplitude, balance, then offset before clamping; PlacementNoise differs in both order and clamping
        public float Sample(float worldX, float worldZ, Vector2[] noiseOffsets)
        {
            if (!IsActive) return 1f;

            var raw = ManagedNoise.SampleByType(noiseType, worldX, worldZ, frequency, noiseOffsets);
            return Mathf.Clamp01(raw * amplitude + balance + offset);
        }
    }
}
