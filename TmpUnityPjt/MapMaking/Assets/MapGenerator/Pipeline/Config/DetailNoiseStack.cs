using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 3段ノイズ合成。MicroVerse の weightNoise/weight2Noise/weight3Noise に相当。
    /// DetailEntry の密度変調で使い、複雑な分布パターンを作る。
    /// </summary>
    [System.Serializable]
    public class DetailNoiseStack
    {
        [Label("プライマリ")]
        public DetailNoiseLayer primary = new DetailNoiseLayer();

        [Label("セカンダリ")]
        public DetailNoiseLayer secondary = new DetailNoiseLayer();
        public NoiseOp secondaryOp = NoiseOp.Multiply;

        [Label("ターシャリ")]
        public DetailNoiseLayer tertiary = new DetailNoiseLayer();
        public NoiseOp tertiaryOp = NoiseOp.Multiply;

        public bool IsActive => primary.IsActive || secondary.IsActive || tertiary.IsActive;

        /// <summary>
        /// 3段ノイズを合成して 0-1 の密度係数を返す。
        /// primary をベースに secondary, tertiary を NoiseOp で合成する。
        /// </summary>
        public float Sample(float worldX, float worldZ, Vector2[] offsets)
        {
            if (!IsActive) return 1f;

            float result = primary.IsActive
                ? primary.Sample(worldX, worldZ, offsets)
                : 1f;

            if (secondary.IsActive)
            {
                float s = secondary.Sample(worldX, worldZ, offsets);
                result = ApplyOp(result, s, secondaryOp);
            }

            if (tertiary.IsActive)
            {
                float t = tertiary.Sample(worldX, worldZ, offsets);
                result = ApplyOp(result, t, tertiaryOp);
            }

            return Mathf.Clamp01(result);
        }

        static float ApplyOp(float a, float b, NoiseOp op)
        {
            switch (op)
            {
                case NoiseOp.Add:      return a + b;
                case NoiseOp.Subtract: return a - b;
                case NoiseOp.Multiply: return a * b;
                // Photoshop式オーバーレイ: 暗部は乗算、明部はスクリーン
                case NoiseOp.Overlay:
                    return a < 0.5f ? 2f * a * b : 1f - 2f * (1f - a) * (1f - b);
                case NoiseOp.Min:      return Mathf.Min(a, b);
                case NoiseOp.Max:      return Mathf.Max(a, b);
                default:               return a * b;
            }
        }
    }
}
