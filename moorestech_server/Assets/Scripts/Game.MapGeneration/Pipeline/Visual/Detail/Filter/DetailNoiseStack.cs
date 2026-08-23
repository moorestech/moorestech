using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Detail.Filter
{
    /// <summary>
    ///     3段ノイズ合成。MapMaking DetailNoiseStack の移植で、Detail密度に空間的ばらつきを与える
    ///     Three-layer noise composition ported from MapMaking's DetailNoiseStack, scattering detail density
    /// </summary>
    public class DetailNoiseStack
    {
        public DetailNoiseLayer primary;
        public DetailNoiseLayer secondary;
        public NoiseOp secondaryOp;
        public DetailNoiseLayer tertiary;
        public NoiseOp tertiaryOp;

        public bool IsActive => primary.IsActive || secondary.IsActive || tertiary.IsActive;

        // primaryを土台にsecondary・tertiaryをNoiseOpで重ねる
        // Layers secondary and tertiary onto primary through their NoiseOp
        public float Sample(float worldX, float worldZ, Vector2[] noiseOffsets)
        {
            if (!IsActive) return 1f;

            var result = primary.IsActive ? primary.Sample(worldX, worldZ, noiseOffsets) : 1f;

            // 合成式はサーバー移植済みのManagedNoise.CombineNoiseと同一なので再実装しない
            // The combination formula matches the already-ported ManagedNoise.CombineNoise, so it is not rewritten
            if (secondary.IsActive)
                result = ManagedNoise.CombineNoise(result, secondary.Sample(worldX, worldZ, noiseOffsets), secondaryOp);

            if (tertiary.IsActive)
                result = ManagedNoise.CombineNoise(result, tertiary.Sample(worldX, worldZ, noiseOffsets), tertiaryOp);

            return Mathf.Clamp01(result);
        }
    }
}
