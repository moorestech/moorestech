using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 単一ノイズレイヤー。MicroVerse の Noise クラスに相当する CPU 実装。
    /// DetailNoiseStack や DetailFilter のノイズ変調で使う。
    /// </summary>
    [System.Serializable]
    public class DetailNoiseLayer
    {
        public MapNoiseType noiseType = MapNoiseType.None;

        [Range(0.1f, 100f)] public float frequency = 10f;
        [Range(0f, 2f)] public float amplitude = 1f;

        // 出力にオフセットを加算してからClamp。分布の底上げ・底下げに使う
        [Range(-1f, 1f)] public float offset = 0f;

        // 0.5 中心のバランス調整。正で明部寄り、負で暗部寄り
        [Range(-0.5f, 0.5f)] public float balance = 0f;

        public bool IsActive => noiseType != MapNoiseType.None;

        /// <summary>
        /// ワールド座標からノイズ値をサンプリングし、offset/balance を適用して 0-1 で返す。
        /// </summary>
        public float Sample(float worldX, float worldZ, Vector2[] offsets)
        {
            if (!IsActive) return 1f;

            float raw = Generators.Util.ManagedNoise.SampleByType(
                noiseType, worldX, worldZ, frequency, offsets);

            // amplitude → balance → offset の順で適用
            float v = raw * amplitude + balance + offset;
            return Mathf.Clamp01(v);
        }
    }
}
