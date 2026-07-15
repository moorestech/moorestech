using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// 岩クラスター周辺の裸地テクスチャ（Mud等）のバイオーム別設定。
    /// TerrainGenerator.ApplyObjectSurroundTexture が参照する。
    /// Phase 5 で実装予定。現時点では定義のみ。
    /// </summary>
    [System.Serializable]
    public class ObjectSurroundTextureConfig
    {
        [Label("有効")]
        public bool enabled = true;

        [Label("周辺テクスチャレイヤー")]
        public TerrainLayer surroundLayer;

        // コア領域（岩直下の強い裸地化）
        [Header("コア領域")]
        [Label("コア半径(m)")]
        [Range(1f, 15f)] public float coreRadius = 5f;

        [Label("コア強度(min)")]
        [Range(0.4f, 1f)] public float coreBlendMin = 0.8f;

        [Label("コア強度(max)")]
        [Range(0.5f, 1f)] public float coreBlendMax = 0.95f;

        // 遷移帯
        [Header("遷移帯")]
        [Label("遷移帯半径(m)")]
        [Range(5f, 30f)] public float transitionRadius = 15f;

        [Label("遷移帯強度(min)")]
        [Range(0.05f, 0.5f)] public float transitionBlendMin = 0.15f;

        [Label("遷移帯強度(max)")]
        [Range(0.2f, 0.8f)] public float transitionBlendMax = 0.5f;

        // ノイズ
        [Header("ノイズ")]
        [Label("低周波ノイズ周波数")]
        [Range(0.01f, 0.1f)] public float noiseLowFrequency = 0.03f;

        [Label("高周波ノイズ周波数")]
        [Range(0.05f, 0.5f)] public float noiseHighFrequency = 0.15f;

        [Label("低周波ノイズ比率")]
        [Range(0f, 1f)] public float noiseLowWeight = 0.6f;

        // フットプリント
        [Header("フットプリント")]
        [Label("岩メッシュ基底幅(m)")]
        [Range(5f, 30f)] public float rockMeshBaseSize = 15f;

        // 非クラスター岩
        [Header("非クラスター岩")]
        [Label("単体裸地半径(m)")]
        [Range(3f, 15f)] public float singleRockRadius = 8f;

        [Label("単体ブレンド強度")]
        [Range(0.2f, 1f)] public float singleRockBlend = 0.6f;
    }
}
