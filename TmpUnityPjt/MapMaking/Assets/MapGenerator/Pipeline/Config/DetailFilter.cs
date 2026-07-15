using MapGenerator.Pipeline.Generators.Util;
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 高さ・傾斜・曲率・角度の共通フィルタ。MicroVerse の FilterSet.Filter に相当。
    /// Simple モードは range+smoothness の台形フィルタ、Curve モードは AnimationCurve で自由応答。
    /// </summary>
    [System.Serializable]
    public class DetailFilter
    {
        public enum Mode
        {
            [InspectorName("シンプル")] Simple,
            [InspectorName("カーブ")]   Curve
        }

        public bool enabled;
        public Mode mode = Mode.Simple;

        // フィルタ結果に掛ける重み。0にすると有効でも効果なし
        [Range(0f, 1f)] public float weight = 1f;

        // Simple モード: この範囲内で1.0、範囲外で0.0（smoothnessで滑らかに遷移）
        public Vector2 range;
        public Vector2 smoothness;

        // フィルタ自体にノイズ変調をかけて、境界線をランダムに揺らす
        [Label("ノイズ変調")]
        public DetailNoiseLayer noise = new DetailNoiseLayer();

        // Curve モード: 入力値(0-1正規化) → 出力値のカスタムマッピング
        public AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.25f, 1f),
            new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));

        /// <summary>
        /// 入力値をフィルタリングして 0-1 の通過率を返す。
        /// worldX/worldZ はノイズ変調のサンプリング座標。
        /// </summary>
        public float Evaluate(float value, float worldX, float worldZ, Vector2[] noiseOffsets)
        {
            if (!enabled) return 1f;

            float result;
            if (mode == Mode.Curve)
            {
                result = curve.Evaluate(value);
            }
            else
            {
                // BurstTerrainMath.FilterRange と同じ台形フィルタ
                result = Jobs.BurstTerrainMath.FilterRange(
                    value, range.x, range.y, smoothness.x, smoothness.y);
            }

            // ノイズで境界を揺らす（ノイズが有効な場合のみ）
            if (noise.IsActive)
            {
                float n = noise.Sample(worldX, worldZ, noiseOffsets);
                result *= n;
            }

            return Mathf.Clamp01(result * weight);
        }

        // --- ファクトリメソッド: 各フィルタ種別のデフォルト値 ---

        public static DetailFilter Height() => new DetailFilter
        {
            range = new Vector2(0f, 1f),
            smoothness = new Vector2(0.02f, 0.02f)
        };

        public static DetailFilter Slope() => new DetailFilter
        {
            range = new Vector2(0f, 90f),
            smoothness = new Vector2(4f, 4f)
        };

        public static DetailFilter Curvature() => new DetailFilter
        {
            range = new Vector2(0f, 1f),
            smoothness = new Vector2(0.1f, 0.1f)
        };

        public static DetailFilter Angle() => new DetailFilter
        {
            range = new Vector2(0f, 360f),
            smoothness = new Vector2(12f, 12f)
        };

        // 距離フィルタ: ワールド単位（メートル）で Tree/Object からの距離を評価
        public static DetailFilter Distance() => new DetailFilter
        {
            range = new Vector2(0f, 50f),
            smoothness = new Vector2(2f, 5f)
        };
    }
}
