using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 岩クラスター周辺に配置する樹木パッチのバイオーム別設定。
    /// PlaceTreesAroundObjects が参照する。
    /// </summary>
    [System.Serializable]
    public class RockProximityTreeConfig
    {
        [Label("有効")]
        public bool enabled = true;

        [Label("パッチ数(min)")]
        [Range(1, 5)] public int patchCountMin = 1;
        [Label("パッチ数(追加max)")]
        [Range(0, 4)] public int patchCountRandom = 2;

        [Label("パッチ距離(min, m)")]
        [Range(3f, 20f)] public float patchDistanceMin = 8f;
        [Label("パッチ距離(追加max, m)")]
        [Range(0f, 15f)] public float patchDistanceRandom = 6f;

        [Label("パッチサイズ(min, m)")]
        [Range(5f, 30f)] public float patchSizeMin = 12f;
        [Label("パッチサイズ(追加max, m)")]
        [Range(0f, 15f)] public float patchSizeRandom = 6f;

        [Label("マスク閾値(min)")]
        [Range(0.1f, 0.6f)] public float maskThresholdMin = 0.32f;
        [Label("マスク閾値(追加max)")]
        [Range(0f, 0.3f)] public float maskThresholdRandom = 0.1f;

        [Label("試行回数(min)")]
        [Range(10, 80)] public int attemptsMin = 40;
        [Label("試行回数(追加max)")]
        [Range(0, 40)] public int attemptsRandom = 21;

        // 岩周辺樹木のスケール範囲。Lerp(low, high, combined)で最終スケール決定
        [Header("スケール範囲")]
        [Label("スケール下限ベース")]
        [Range(0.1f, 1f)] public float scaleLowBase = 0.5f;

        [Label("スケール下限幅")]
        [Range(0f, 1f)] public float scaleLowRange = 0.3f;

        [Label("スケール上限ベース")]
        [Range(0.1f, 2f)] public float scaleHighBase = 0.8f;

        [Label("スケール上限幅")]
        [Range(0f, 1f)] public float scaleHighRange = 0.4f;

        // マスクノイズパラメータ（PlaceTreesAroundObjects内のPerlinNoise制御）
        [Header("マスクノイズ")]
        [Label("粗マスク周波数")]
        [Range(0.01f, 0.3f)] public float maskCoarseFrequency = 0.06f;

        [Label("細マスク周波数")]
        [Range(0.01f, 0.5f)] public float maskFineFrequency = 0.18f;

        [Label("粗マスク重み")]
        [Range(0f, 1f)] public float maskCoarseWeight = 0.65f;

        [Label("距離ペナルティ係数")]
        [Range(0f, 1.5f)] public float distancePenaltyFactor = 0.5f;
    }
}
