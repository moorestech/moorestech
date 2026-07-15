using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// バイオーム境界・ブレンドの共通設定。
    /// テクスチャブレンド・高さブレンド・境界ノイズを全バイオーム共通で制御する。
    /// </summary>
    [System.Serializable]
    public class BiomeBoundaryConfig
    {
        // バイオーム境界でのテクスチャ混合強度。0=勝者のみ、1=完全ブレンド
        [Header("テクスチャブレンド")]
        [Label("テクスチャブレンド強度")]
        [Range(0f, 1f)] public float textureBlendStrength = 0.6f;

        // 高さブレンドの高速パス閾値。この重み以上で単一バイオーム処理に切り替え
        [Header("高さブレンド")]
        [Label("高速パス閾値")]
        [Range(0.8f, 1f)] public float heightBlendFastPathThreshold = 0.95f;

        // 2番目のバイオームがこの重み以下ならブレンドをスキップ
        [Label("ブレンド最低重み")]
        [Range(0f, 0.1f)] public float heightBlendMinWeight = 0.01f;

        // ボックスブラー半径 = biomeBlendRadius / この値。小さいほどブレンド幅が広がる
        [Header("重みブラー")]
        [Label("ブラー半径除数")]
        [Range(1, 8)] public int blurRadiusDivisor = 2;

        // BoundaryNoiseJobの傾斜マスクsmoothstep遷移幅（度）
        [Header("境界ノイズ")]
        [Label("Smoothstep遷移幅(°)")]
        [Range(1f, 30f)] public float boundaryNoiseSmoothstepWidth = 15f;

        // 2帯域ノイズの混合比（中周波＋高周波=1.0を推奨）
        [Label("中周波ノイズ重み")]
        [Range(0f, 1f)] public float boundaryNoiseMidWeight = 0.7f;

        [Label("高周波ノイズ重み")]
        [Range(0f, 1f)] public float boundaryNoiseHighWeight = 0.3f;

    }
}
